import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** One navigation state for every workspace route. The compact rail stays operable. */
@Injectable({ providedIn: 'root' })
export class WorkspaceLayout {
  private readonly media = window.matchMedia('(max-width: 859px)');
  readonly narrow = signal(this.media.matches);
  readonly compact = signal(this.media.matches);
  readonly overlay = computed(() => this.narrow() && !this.compact());
  constructor() {
    const resize = () => {
      this.narrow.set(this.media.matches);
      if (this.media.matches) this.compact.set(true);
    };
    this.media.addEventListener('change', resize);
    inject(DestroyRef).onDestroy(() => this.media.removeEventListener('change', resize));
  }
  toggle() {
    this.compact.update((value) => !value);
  }
  closeMobile() {
    if (this.narrow()) this.compact.set(true);
  }
}
