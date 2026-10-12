import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { below } from '../../shared/browser/breakpoints';
import { mediaQuery } from '../../shared/browser/media-query';

/** Desktop density and mobile disclosure are independent across workspace routes. */
@Injectable({ providedIn: 'root' })
export class WorkspaceLayout {
  readonly narrow = mediaQuery(below('narrow'));
  readonly desktopCompact = signal(false);
  readonly mobileOpen = signal(false);
  readonly compact = computed(() => !this.narrow() && this.desktopCompact());
  readonly overlay = computed(() => this.narrow() && this.mobileOpen());
  readonly expanded = computed(() => (this.narrow() ? this.mobileOpen() : !this.compact()));
  private opener: HTMLElement | null = null;
  private focusFrame = 0;
  constructor() {
    // Crossing the breakpoint closes the drawer; focus left in the sidebar moves to the visible toggle.
    let narrow = this.narrow();
    effect(() => {
      if (this.narrow() === narrow) return;
      narrow = this.narrow();
      untracked(() => {
        const focusInSidebar = document.activeElement?.closest('.workspace-sidebar');
        this.mobileOpen.set(false);
        if (focusInSidebar) this.scheduleFocus();
      });
    });
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.focusFrame));
  }
  toggle() {
    if (!this.narrow()) {
      this.desktopCompact.update((value) => !value);
      return;
    }
    if (this.mobileOpen()) this.closeMobile();
    else {
      cancelAnimationFrame(this.focusFrame);
      this.opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      this.mobileOpen.set(true);
    }
  }
  closeMobile() {
    const wasOpen = this.mobileOpen();
    this.mobileOpen.set(false);
    if (wasOpen) this.scheduleFocus();
  }
  private scheduleFocus() {
    cancelAnimationFrame(this.focusFrame);
    this.focusFrame = requestAnimationFrame(() => {
      if (!this.overlay()) this.restoreFocus();
    });
  }
  private restoreFocus() {
    const opener =
      this.opener?.isConnected && this.opener.checkVisibility() && !this.opener.closest('[inert]')
        ? this.opener
        : document.querySelector<HTMLElement>(
            this.narrow() ? '.workspace-menu-button' : '.sidebar-toggle',
          );
    opener?.focus({ preventScroll: true });
    this.opener = null;
  }
}
