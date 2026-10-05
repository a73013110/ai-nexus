import { Injectable, inject } from '@angular/core';
import { Location } from '@angular/common';
import { NavigationStart, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WORKSPACE_HOME } from '../../core/workspace-home';

export interface ReaderOrigin {
  url: string;
  scrollTop: number;
  documentId: string;
  messageId?: string;
  navigationId: number;
}

/** Only workspace destinations are valid, including when a reader URL is shared. */
export function readerReturnUrl(value: unknown): string | null {
  if (
    typeof value !== 'string' ||
    value.length > 2048 ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    /[\u0000-\u001f\u007f]/.test(value)
  )
    return null;
  const url = new URL(value, 'https://nexus.invalid');
  if (
    url.origin !== 'https://nexus.invalid' ||
    !/^\/(?:dashboard|chat|files|knowledge|projects|artifacts|repositories|quality|shared|tasks|integrations|admin|settings)(?:\/[a-z0-9-]+)?$/i.test(
      url.pathname,
    )
  )
    return null;
  return url.pathname + url.search + url.hash;
}

export function readerReturnLabel(url: string) {
  if (/^\/files(?:[/?#]|$)/.test(url)) return '返回檔案庫';
  if (/^\/chat(?:[/?#]|$)/.test(url)) return '返回對話';
  if (/^\/projects(?:[/?#]|$)/.test(url)) return '返回專案';
  if (/^\/knowledge(?:[/?#]|$)/.test(url)) return '返回知識庫';
  if (url === WORKSPACE_HOME) return '前往總覽';
  return '返回上一頁';
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
        // Covers the browser Back button as well as the reader's own return action.
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
