import { DestroyRef, Injectable, effect, inject, untracked } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { featureForUrl } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';
import { BrowserSession } from './browser-session';

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
    for (const event of ['pointerdown', 'keydown', 'wheel'])
      document.addEventListener(event, interacted, { passive: true });
    document.addEventListener('visibilitychange', visibility);
    this.router.events
      .pipe(
        filter((event2) => event2 instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.schedule(Math.max(0, 5000 - (Date.now() - this.lastHeartbeat))));
    effect(() => {
      const owner = this.session.me()?.id ?? '';
      this.session.auth.generation();
      untracked(() => {
        ++this.revision;
        this.controller?.abort();
        clearTimeout(this.timer);
        this.owner = owner;
        this.lastInteraction = Date.now();
        if (owner) this.schedule(0);
      });
    });
    destroy.onDestroy(() => {
      ++this.revision;
      clearTimeout(this.timer);
      this.controller?.abort();
      for (const event of ['pointerdown', 'keydown', 'wheel'])
        document.removeEventListener(event, interacted);
      document.removeEventListener('visibilitychange', visibility);
    });
  }
  private schedule(delay: number) {
    clearTimeout(this.timer);
    if (this.owner) this.timer = setTimeout(() => void this.heartbeat(), delay);
  }
  private async heartbeat() {
    const feature = presenceFeature(this.router.url);
    if (!this.owner || !feature) {
      this.schedule(25000);
      return;
    }
    const revision = ++this.revision;
    this.controller?.abort();
    this.controller = new AbortController();
    const state =
      document.visibilityState === 'hidden'
        ? 'background'
        : Date.now() - this.lastInteraction > 120000
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
    if (revision === this.revision && enabled) this.schedule(25000);
  }
}
