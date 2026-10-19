import { DestroyRef, Injectable, effect, inject, untracked } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { featureForUrl } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';
import { BrowserSession } from './browser-session';

const HEARTBEAT_MS = 25_000;
/** 導覽後最快多久再送一次心跳，連續換頁時不必每頁都送。 */
const NAVIGATION_GAP_MS = 5000;
/** 超過這段時間沒有操作就回報為閒置。 */
const IDLE_MS = 120_000;
const INTERACTIONS = ['pointerdown', 'keydown', 'wheel'] as const;

/**
 * 在線狀態只回報目前網址在功能目錄中的 ID（頁面或閱讀器），個人設定回報 `settings`；
 * 資源 ID、查詢字串與不在目錄中的網址都不送出（後端也只接受這套詞彙）。
 */
export function presenceFeature(url: string): string | null {
  const path = url.split(/[?#;]/)[0];
  if (path === '/settings') return 'settings';
  return featureForUrl(path)?.id ?? null;
}

/** 單一應用程式心跳，只記錄固定的頁面類別與活動狀態。 */
@Injectable({ providedIn: 'root' })
export class BrowserPresence {
  private readonly api = inject(ApiClient);
  private readonly session = inject(WorkspaceSession);
  private readonly router = inject(Router);
  private readonly tab = inject(BrowserSession);
  private timer?: ReturnType<typeof setTimeout>;
  private controller?: AbortController;
  private owner = '';
  private revision = 0;
  private lastInteraction = Date.now();
  private lastHeartbeat = 0;
  constructor() {
    const destroy = inject(DestroyRef);
    const interacted = () => {
      this.lastInteraction = Date.now();
    };
    const visibility = () => this.schedule(0);
    for (const event of INTERACTIONS)
      document.addEventListener(event, interacted, { passive: true });
    document.addEventListener('visibilitychange', visibility);
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() =>
        this.schedule(Math.max(0, NAVIGATION_GAP_MS - (Date.now() - this.lastHeartbeat))),
      );
    effect(() => {
      const owner = this.session.me()?.id ?? '';
      this.session.auth.generation();
      untracked(() => {
        this.stop();
        this.owner = owner;
        this.lastInteraction = Date.now();
        if (owner) this.schedule(0);
      });
    });
    destroy.onDestroy(() => {
      this.stop();
      for (const event of INTERACTIONS) document.removeEventListener(event, interacted);
      document.removeEventListener('visibilitychange', visibility);
    });
  }
  /** 作廢進行中與排定的心跳；晚到的回應因 `revision` 不同而不再排下一次。 */
  private stop() {
    ++this.revision;
    this.controller?.abort();
    clearTimeout(this.timer);
  }
  private schedule(delay: number) {
    clearTimeout(this.timer);
    if (this.owner) this.timer = setTimeout(() => void this.heartbeat(), delay);
  }
  private async heartbeat() {
    const feature = presenceFeature(this.router.url);
    if (!this.owner || !feature) {
      this.schedule(HEARTBEAT_MS);
      return;
    }
    const revision = ++this.revision;
    this.controller?.abort();
    this.controller = new AbortController();
    const state =
      document.visibilityState === 'hidden'
        ? 'background'
        : Date.now() - this.lastInteraction > IDLE_MS
          ? 'idle'
          : 'active';
    this.lastHeartbeat = Date.now();
    let enabled = true;
    try {
      const result = await this.api.post('/api/v1/presence', {
        body: { sessionId: this.tab.id, feature, state },
        signal: this.controller.signal,
      });
      enabled = result.enabled;
    } catch {
      /* 在線狀態只供參考；遙測失敗絕不中斷使用者的工作。 */
    }
    if (revision === this.revision && enabled) this.schedule(HEARTBEAT_MS);
  }
}
