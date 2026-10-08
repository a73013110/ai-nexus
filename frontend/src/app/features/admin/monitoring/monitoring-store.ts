import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { ApiTransport, ApiError } from '../../../core/api/api-transport';
import type { components } from '../../../core/api/schema';
import { jsonEvents } from '../../../core/stream/json-events';
import { safeMessage } from '../../../core/api/safe-errors';

export type MonitoringSnapshot = components['schemas']['MonitoringSnapshot'];
export type OnlineSession = components['schemas']['OnlineSession'];
export type DependencyTraffic = components['schemas']['DependencyTraffic'];

@Injectable()
export class MonitoringStore {
  private readonly http = inject(ApiTransport);
  readonly snapshot = signal<MonitoringSnapshot | null>(null);
  readonly connection = signal<
    'connecting' | 'live' | 'reconnecting' | 'paused' | 'hidden' | 'denied'
  >('connecting');
  readonly error = signal('');
  readonly minutes = signal(5);
  readonly paused = signal(false);
  readonly now = signal(Date.now());
  private controller?: AbortController;
  private retry?: ReturnType<typeof setTimeout>;
  private watchdog?: ReturnType<typeof setTimeout>;
  private revision = 0;
  private retries = 0;
  private started = false;
  constructor() {
    const visibility = () => {
      if (this.started) this.restart();
    };
    document.addEventListener('visibilitychange', visibility);
    const clock = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => {
      this.stop();
      clearInterval(clock);
      document.removeEventListener('visibilitychange', visibility);
    });
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
    this.restart();
  }
  refresh() {
    this.retries = 0;
    this.restart();
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
    try {
      this.armWatchdog();
      const response = await this.http.response(
        `/admin/monitoring/events?minutes=${this.minutes()}`,
        'GET',
        undefined,
        { Accept: 'text/event-stream' },
        signal,
      );
      for await (const snapshot of jsonEvents<MonitoringSnapshot>(response, 'snapshot', signal)) {
        if (signal.aborted || revision !== this.revision) return;
        if (
          snapshot.version !== 1 ||
          !Array.isArray(snapshot.sessions) ||
          !Array.isArray(snapshot.timeline) ||
          !Number.isFinite(Date.parse(snapshot.at))
        )
          throw new Error('監控資料格式不正確');
        this.snapshot.set(snapshot);
        this.now.set(Date.now());
        this.connection.set('live');
        this.error.set('');
        this.retries = 0;
        this.armWatchdog(Math.max(15000, snapshot.refreshSeconds * 4000));
      }
      if (!signal.aborted) throw new Error('事件串流已結束');
    } catch (error) {
      if (revision !== this.revision) return;
      clearTimeout(this.watchdog);
      if (error instanceof ApiError && [401, 403].includes(error.status)) {
        this.connection.set('denied');
        this.snapshot.set(null);
        this.error.set(safeMessage(error));
        return;
      }
      this.connection.set('reconnecting');
      this.error.set(safeMessage(error));
      const delay = Math.min(1000 * 2 ** this.retries++, 30000);
      this.retry = setTimeout(() => this.restart(), delay);
    }
  }
  private armWatchdog(ms = 20000) {
    clearTimeout(this.watchdog);
    this.watchdog = setTimeout(() => this.controller?.abort(), ms);
  }
  export() {
    return this.http.response(`/admin/monitoring/export?minutes=${this.minutes()}`);
  }
}
