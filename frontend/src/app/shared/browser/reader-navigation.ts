import { Injectable, inject } from '@angular/core';
import { Location } from '@angular/common';
import { NavigationStart, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { featureForUrl } from '@core/api/catalog';
import { WORKSPACE_HOME } from '@core/layout/workspace-home';

export interface ReaderOrigin {
  url: string;
  scrollTop: number;
  documentId: string;
  messageId?: string;
  navigationId: number;
}

/** 只有工作區的目的地有效，即使閱讀器的 URL 被分享也一樣。 */
export function readerReturnUrl(value: unknown): string | null {
  if (
    typeof value !== 'string' ||
    value.length > 2048 ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    // eslint-disable-next-line no-control-regex -- rejects control characters in return URLs
    /[\u0000-\u001f\u007f]/.test(value)
  )
    return null;
  const url = new URL(value, 'https://app.invalid');
  if (url.origin !== 'https://app.invalid') return null;
  // 只收目錄中的頁面（不收閱讀器，避免閱讀器互相返回），頁面網址之後最多一段 ID。
  const feature = featureForUrl(url.pathname);
  const rest = feature && url.pathname.slice(feature.route.length);
  if (feature?.kind !== 'page' || rest === undefined || !/^(?:\/[a-z0-9-]+)?$/i.test(rest))
    return null;
  return url.pathname + url.search + url.hash;
}

/** 返回按鈕的文字：返回來源頁所屬的功能；首頁寫「前往總覽」，不在目錄的網址寫「返回上一頁」。 */
export function readerReturnLabel(url: string) {
  if (url === WORKSPACE_HOME) return '前往總覽';
  const feature = featureForUrl(url);
  return feature?.kind === 'page' ? `返回${feature.name}` : '返回上一頁';
}

@Injectable({ providedIn: 'root' })
export class ReaderNavigation {
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private active: ReaderOrigin | null = null;
  private returning: ReaderOrigin | null = null;

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationStart && this.active) {
        // 同時涵蓋瀏覽器的上一頁按鈕與閱讀器自己的返回動作。
        if (event.url === this.active.url) this.returning = this.active;
        this.active = null;
      }
    });
  }

  capture(documentId: string, element: HTMLElement): ReaderOrigin {
    const root = element.closest('.main-workspace');
    const scroll =
      root?.querySelector<HTMLElement>('.conversation-viewport') ??
      element.closest<HTMLElement>('.feature-main');
    return {
      url: this.router.url,
      scrollTop: scroll?.scrollTop ?? 0,
      documentId,
      messageId: element.closest<HTMLElement>('[data-message-id]')?.dataset['messageId'],
      navigationId: history.state?.navigationId ?? 0,
    };
  }

  enter(returnTo: string | null) {
    const origin = history.state?.readerOrigin as ReaderOrigin | undefined;
    this.active =
      origin &&
      readerReturnUrl(origin.url) === returnTo &&
      Number.isFinite(origin.scrollTop) &&
      origin.scrollTop >= 0 &&
      origin.scrollTop < 10_000_000 &&
      typeof origin.documentId === 'string' &&
      Number.isInteger(origin.navigationId) &&
      origin.navigationId > 0
        ? origin
        : null;
  }

  back(url: string) {
    if (this.active?.url === url && history.length > 1) this.location.back();
    else void this.router.navigateByUrl(url, { replaceUrl: true });
  }

  takeReturn(url: string): ReaderOrigin | null {
    const origin = this.returning;
    this.returning = null;
    return origin?.url === url ? origin : null;
  }
}
