import { computed, effect, Injectable, signal, untracked } from '@angular/core';
import { mediaQuery } from '../../shared/browser/media-query';
import type { PreferencesDto } from '../api/schema';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preferences = signal<PreferencesDto>({
    theme: 'system',
    reducedMotion: false,
    defaultModelId: null,
  });
  private readonly dark = mediaQuery('(prefers-color-scheme: dark)');
  private readonly systemReduced = mediaQuery('(prefers-reduced-motion: reduce)');
  readonly reducedMotion = computed(() => this.preferences().reducedMotion || this.systemReduced());
  constructor() {
    // Follows the system scheme; preference changes render synchronously through apply().
    effect(() => {
      this.dark();
      untracked(() => this.render());
    });
    try {
      const local = JSON.parse(
        localStorage.getItem('nexus.appearance') ?? 'null',
      ) as PreferencesDto | null;
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
  apply(value: PreferencesDto) {
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
      value.theme === 'system' ? (this.dark() ? 'dark' : 'light') : value.theme;
    document.documentElement.dataset['reducedMotion'] = String(value.reducedMotion);
  }
}
