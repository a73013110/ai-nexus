import { Injectable, signal } from '@angular/core';
import type { Preferences } from '../api/types';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preferences = signal<Preferences>({
    theme: 'system',
    reducedMotion: false,
    defaultModelId: null,
  });
  private readonly dark = window.matchMedia('(prefers-color-scheme: dark)');
  constructor() {
    this.dark.addEventListener('change', () => this.render());
    try {
      const local = JSON.parse(
        localStorage.getItem('nexus.appearance') ?? 'null',
      ) as Preferences | null;
      if (local && ['system', 'light', 'dark'].includes(local.theme))
        this.preferences.set({
          theme: local.theme,
          reducedMotion: !!local.reducedMotion,
          defaultModelId: null,
        });
    } catch {
      /* Appearance is optional; storage can be disabled by policy. */
    }
    this.render();
  }
  apply(value: Preferences) {
    this.preferences.set(value);
    this.render();
    try {
      localStorage.setItem(
        'nexus.appearance',
        JSON.stringify({ theme: value.theme, reducedMotion: value.reducedMotion }),
      );
    } catch {}
  }
  private render() {
    const value = this.preferences();
    document.documentElement.dataset['theme'] =
      value.theme === 'system' ? (this.dark.matches ? 'dark' : 'light') : value.theme;
    document.documentElement.dataset['reducedMotion'] = String(value.reducedMotion);
  }
}
