import { computed, DestroyRef, Injectable, inject, signal } from '@angular/core';
import { ApiError, safeMessage } from '@core/errors/safe-errors';
import type {
  DependencyTraffic,
  HealthSummary,
  MonitoredSubsystem,
  MonitoringSnapshot,
  ResourcePoint,
  TrafficPoint,
} from '@core/api/schema';
import { jsonEvents } from '@core/stream/json-events';
import { MonitoringApi } from './monitoring-api';
import { healthAnnouncement } from './monitoring-format';

const arrays = [
  'sessions',
  'timeline',
  'resourceTimeline',
  'activities',
  'dependencies',
  'endpoints',
  'subsystems',
] as const;

/**
 * Accepts version 2 snapshots only and normalises the parts a newer server might leave out, so a
 * malformed frame fails loudly (and reconnects) instead of rendering half a page.
 */
export function readSnapshot(value: unknown): MonitoringSnapshot {
  const snapshot = value as MonitoringSnapshot | null;
  if (
    !snapshot ||
    typeof snapshot !== 'object' ||
    snapshot.version !== 2 ||
    arrays.some((key) => !Array.isArray(snapshot[key])) ||
    !snapshot.health ||
    !Array.isArray(snapshot.health.issues) ||
    !snapshot.resources ||
    !snapshot.traffic ||
    !Number.isFinite(Date.parse(snapshot.at))
  )
    throw new Error('監控資料格式不正確');
  return {
    ...snapshot,
    recentTraffic: snapshot.recentTraffic ?? snapshot.traffic,
    subsystems: snapshot.subsystems.map((s) => ({
      ...s,
      facts: Array.isArray(s.facts) ? s.facts : [],
    })),
  };
}

/** Series compare by their newest completed bucket: a bucket never changes once it is closed. */
const sameSeries = (
  a: readonly (TrafficPoint | ResourcePoint)[],
  b: readonly (TrafficPoint | ResourcePoint)[],
) =>
  a === b ||
  (a.length === b.length && a.at(0)?.at === b.at(0)?.at && a.at(-1)?.at === b.at(-1)?.at);
const healthKey = (health: HealthSummary | null) =>
  health
    ? health.status +
      '|' +
      health.issues
        .map((i) => [i.code, i.severity, i.subject, i.title, i.detail].join('·'))
        .join('|')
    : '';
const sameJson = <T>(a: T, b: T) => a === b || JSON.stringify(a) === JSON.stringify(b);

/**
 * Speaks only once a new health state holds for two snapshots in a row, so a value hovering at a
 * threshold does not chatter; the first settled state is the page itself and is not announced.
 */
export class AnnouncementGate {
  private pending = '';
  private settled: string | null = null;
  /** Returns the text to announce, or null. */
  observe(key: string, text: string): string | null {
    if (key !== this.pending) {
      this.pending = key;
      return null;
    }
    if (key === this.settled) return null;
    const first = this.settled === null;
    this.settled = key;
    return first ? null : text;
  }
}

@Injectable()
export class MonitoringStore {
  private readonly api = inject(MonitoringApi);
  readonly snapshot = signal<MonitoringSnapshot | null>(null);
  readonly connection = signal<
    'connecting' | 'live' | 'reconnecting' | 'paused' | 'hidden' | 'denied'
  >('connecting');
  readonly error = signal('');
  readonly minutes = signal(5);
  readonly paused = signal(false);
  readonly now = signal(Date.now());
  /** The polite announcement of a settled health change (see `AnnouncementGate`). */
  readonly announcement = signal('');

  // Slices keep their reference while their content is unchanged, so a 3-second snapshot only
  // re-renders the parts that actually moved.
  readonly health = computed(() => this.snapshot()?.health ?? null, {
    equal: (a, b) => healthKey(a) === healthKey(b),
  });
  readonly timeline = computed<readonly TrafficPoint[]>(() => this.snapshot()?.timeline ?? [], {
    equal: sameSeries,
  });
  readonly resourceTimeline = computed<readonly ResourcePoint[]>(
    () => this.snapshot()?.resourceTimeline ?? [],
    { equal: sameSeries },
  );
  readonly dependencies = computed<readonly DependencyTraffic[]>(
    () => this.snapshot()?.dependencies ?? [],
    { equal: sameJson },
  );
  readonly subsystems = computed<readonly MonitoredSubsystem[]>(
    () => this.snapshot()?.subsystems ?? [],
    { equal: sameJson },
  );
  readonly issueCount = computed(() => this.health()?.issues.length ?? 0);
  readonly worstSeverity = computed<'critical' | 'warning' | null>(() => {
    const issues = this.health()?.issues ?? [];
    return issues.some((i) => i.severity === 'critical')
      ? 'critical'
      : issues.length
        ? 'warning'
        : null;
  });
  /** Data older than four refreshes, or no live stream, is shown as such. */
  readonly stale = computed(() => {
    const snapshot = this.snapshot();
    return (
      this.connection() !== 'live' ||
      !snapshot ||
      this.now() - Date.parse(snapshot.at) > Math.max(15000, snapshot.refreshSeconds * 4000)
    );
  });

  private readonly gate = new AnnouncementGate();
  private controller?: AbortController;
  private retry?: ReturnType<typeof setTimeout>;
  private watchdog?: ReturnType<typeof setTimeout>;
  private clock?: ReturnType<typeof setInterval>;
  private revision = 0;
  private retries = 0;
  private started = false;
  constructor() {
    const visibility = () => {
      this.tick();
      if (this.started) this.restart();
    };
    document.addEventListener('visibilitychange', visibility);
    this.tick();
    inject(DestroyRef).onDestroy(() => {
      this.stop();
      clearInterval(this.clock);
      document.removeEventListener('visibilitychange', visibility);
    });
  }
  /** The one-second clock behind staleness and uptime; it stops while the tab is hidden. */
  private tick() {
    clearInterval(this.clock);
    this.now.set(Date.now());
    if (document.visibilityState !== 'hidden')
      this.clock = setInterval(() => this.now.set(Date.now()), 1000);
  }
  start() {
    this.started = true;
    this.restart();
  }
  setWindow(value: string) {
    this.minutes.set(Number(value));
    this.retries = 0;
    this.restart();
  }
  toggle() {
    this.paused.update((v) => !v);
    this.error.set('');
    this.restart();
  }
  refresh() {
    this.paused.set(false);
    this.error.set('');
    this.retries = 0;
    this.restart();
  }
  /** Applies one snapshot from the stream. */
  receive(value: unknown) {
    const snapshot = readSnapshot(value);
    this.snapshot.set(snapshot);
    this.now.set(Date.now());
    const said = this.gate.observe(
      snapshot.health.status +
        ':' +
        snapshot.health.issues.map((i) => i.code + '/' + i.subject).join(','),
      healthAnnouncement(snapshot.health),
    );
    if (said) this.announcement.set(said);
    return snapshot;
  }
  private stop() {
    ++this.revision;
    this.controller?.abort();
    clearTimeout(this.retry);
    clearTimeout(this.watchdog);
  }
  private restart() {
    this.stop();
    if (this.paused()) {
      this.connection.set('paused');
      return;
    }
    if (document.visibilityState === 'hidden') {
      this.connection.set('hidden');
      return;
    }
    this.controller = new AbortController();
    void this.follow(this.revision, this.controller.signal);
  }
  private async follow(revision: number, signal: AbortSignal) {
    this.connection.set(this.snapshot() ? 'reconnecting' : 'connecting');
    let received = false;
    try {
      this.armWatchdog();
      const response = await this.api.events(this.minutes(), signal);
      for await (const frame of jsonEvents<unknown>(response, 'snapshot', signal)) {
        if (signal.aborted || revision !== this.revision) return;
        const snapshot = this.receive(frame);
        received = true;
        this.connection.set('live');
        this.error.set('');
        this.retries = 0;
        this.armWatchdog(Math.max(15000, snapshot.refreshSeconds * 4000));
      }
      if (signal.aborted || revision !== this.revision) return;
      // The server ends every stream after ten minutes so the cookie is checked again; that is not an error.
      if (received) {
        this.restart();
        return;
      }
      throw new Error('事件串流已結束');
    } catch (error) {
      if (revision !== this.revision) return;
      clearTimeout(this.watchdog);
      if (error instanceof ApiError && [401, 403].includes(error.status)) {
        this.connection.set('denied');
        this.snapshot.set(null);
        this.error.set(safeMessage(error));
        return;
      }
      // A newer stream of this account (another window) took over; reconnecting would take it back in turn.
      if (error instanceof ApiError && error.code === 'stream_superseded') {
        this.paused.set(true);
        this.connection.set('paused');
        this.error.set(safeMessage(error));
        return;
      }
      this.connection.set('reconnecting');
      this.error.set(safeMessage(error));
      const backoff = Math.min(1000 * 2 ** this.retries++, 30000);
      const asked =
        error instanceof ApiError && error.status === 429 ? (error.retryAfter ?? 0) * 1000 : 0;
      this.retry = setTimeout(() => this.restart(), Math.max(backoff, asked));
    }
  }
  private armWatchdog(ms = 20000) {
    clearTimeout(this.watchdog);
    this.watchdog = setTimeout(() => this.controller?.abort(), ms);
  }
  export() {
    return this.api.export(this.minutes());
  }
}
