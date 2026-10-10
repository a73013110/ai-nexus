import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** Desktop density and mobile disclosure are independent across workspace routes. */
@Injectable({ providedIn: 'root' })
export class WorkspaceLayout {
  private readonly media = window.matchMedia('(max-width: 859px)');
  readonly narrow = signal(this.media.matches);
  readonly desktopCompact = signal(false);
  readonly mobileOpen = signal(false);
  readonly compact = computed(() => !this.narrow() && this.desktopCompact());
  readonly overlay = computed(() => this.narrow() && this.mobileOpen());
  readonly expanded = computed(() => (this.narrow() ? this.mobileOpen() : !this.compact()));
  private opener: HTMLElement | null = null;
  private focusFrame = 0;
  constructor() {
    const resize = () => {
      const focusInSidebar = document.activeElement?.closest('.workspace-sidebar');
      this.narrow.set(this.media.matches);
      this.mobileOpen.set(false);
      if (focusInSidebar) this.scheduleFocus();
    };
    this.media.addEventListener('change', resize);
    inject(DestroyRef).onDestroy(() => {
      this.media.removeEventListener('change', resize);
      cancelAnimationFrame(this.focusFrame);
    });
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
