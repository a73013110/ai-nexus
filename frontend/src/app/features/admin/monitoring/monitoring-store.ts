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
 * 只接受版本 2 的快照，並把較新伺服器可能省略的部分補齊；格式錯誤的串流幀會明確失敗（並重新連線），
 * 而不會只渲染半個頁面。
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
    subsystems: snapshot.subsystems.map((segment) => ({
      ...segment,
      facts: Array.isArray(segment.facts) ? segment.facts : [],
    })),
  };
}

/** 數列以最新完成的桶比較：桶關閉後就不會再變。 */
const sameSeries = (
  first: readonly (TrafficPoint | ResourcePoint)[],
  second: readonly (TrafficPoint | ResourcePoint)[],
) =>
  first === second ||
  (first.length === second.length &&
    first.at(0)?.at === second.at(0)?.at &&
    first.at(-1)?.at === second.at(-1)?.at);
const healthKey = (health: HealthSummary | null) =>
  health
    ? health.status +
      '|' +
      health.issues
        .map((issue) =>
          [issue.code, issue.severity, issue.subject, issue.title, issue.detail].join('·'),
        )
        .join('|')
    : '';
const sameJson = <T>(first: T, second: T) =>
  first === second || JSON.stringify(first) === JSON.stringify(second);
/** 超過四次更新沒收到快照就算過期，至少 15 秒；過期顯示與看門狗共用同一個門檻。 */
const staleAfter = (snapshot: MonitoringSnapshot) =>
  Math.max(15000, snapshot.refreshSeconds * 4000);

/**
 * 只有新的健康狀態連續兩次快照都成立才發聲，避免數值在門檻附近來回跳動；
 * 第一次穩定的狀態就是頁面本身，不會播報。
 */
export class AnnouncementGate {
  private pending = '';
  private settled: string | null = null;
  /** 回傳要播報的文字，或 null。 */
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
  /** 穩定的健康狀態變化的禮貌播報（見 `AnnouncementGate`）。 */
  readonly announcement = signal('');

  // 切片在內容沒變時保留參考，所以每 3 秒的快照只重新渲染真正變動的部分。
  readonly health = computed(() => this.snapshot()?.health ?? null, {
    equal: (first, second) => healthKey(first) === healthKey(second),
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
  /** 資料超過四次更新，或沒有即時串流時，會以此狀態顯示。 */
  readonly stale = computed(() => {
    const snapshot = this.snapshot();
    return (
      this.connection() !== 'live' ||
      !snapshot ||
      this.now() - Date.parse(snapshot.at) > staleAfter(snapshot)
    );
  });

  private readonly gate = new AnnouncementGate();
  private controller?: AbortController;
  private retry?: ReturnType<typeof setTimeout>;
  private watchdog?: ReturnType<typeof setTimeout>;
  private clock?: ReturnType<typeof setInterval>;
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
  /** 過期判斷與運行時間所依賴的一秒時鐘；分頁隱藏時停止。 */
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
    this.paused.update((flag) => !flag);
    this.error.set('');
    this.restart();
  }
  refresh() {
    this.paused.set(false);
    this.error.set('');
    this.retries = 0;
    this.restart();
  }
  /** 套用來自串流的一份快照。 */
  receive(value: unknown) {
    const snapshot = readSnapshot(value);
    this.snapshot.set(snapshot);
    this.now.set(Date.now());
    const said = this.gate.observe(
      snapshot.health.status +
        ':' +
        snapshot.health.issues.map((issue) => issue.code + '/' + issue.subject).join(','),
      healthAnnouncement(snapshot.health),
    );
    if (said) this.announcement.set(said);
    return snapshot;
  }
  /** 換掉 controller 就讓舊的 `follow` 失效；看門狗只中止、不換，因此它中止的那條會進入重連。 */
  private stop() {
    this.controller?.abort();
    this.controller = undefined;
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
    void this.follow(this.controller.signal);
  }
  private async follow(signal: AbortSignal) {
    this.connection.set(this.snapshot() ? 'reconnecting' : 'connecting');
    let received = false;
    try {
      this.armWatchdog();
      const response = await this.api.events(this.minutes(), signal);
      for await (const frame of jsonEvents<unknown>(response, 'snapshot', signal)) {
        if (signal.aborted) return;
        const snapshot = this.receive(frame);
        received = true;
        this.connection.set('live');
        this.error.set('');
        this.retries = 0;
        this.armWatchdog(staleAfter(snapshot));
      }
      if (signal.aborted) return;
      // 伺服器每十分鐘會結束一次串流，好重新檢查 cookie；這不是錯誤。
      if (received) {
        this.restart();
        return;
      }
      throw new Error('事件串流已結束');
    } catch (error) {
      if (signal !== this.controller?.signal) return;
      clearTimeout(this.watchdog);
      if (error instanceof ApiError && [401, 403].includes(error.status)) {
        this.connection.set('denied');
        this.snapshot.set(null);
        this.error.set(safeMessage(error));
        return;
      }
      // 同一帳號的較新串流（另一個視窗）已接手；此時重新連線會反過來搶回。
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
