import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import type { PreferencesDto } from '../api/schema';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preferences = signal<PreferencesDto>({
    theme: 'system',
    reducedMotion: false,
    defaultModelId: null,
  });
  private readonly dark = window.matchMedia('(prefers-color-scheme: dark)');
  private readonly motion = window.matchMedia('(prefers-reduced-motion: reduce)');
  private readonly systemReduced = signal(this.motion.matches);
  readonly reducedMotion = computed(() => this.preferences().reducedMotion || this.systemReduced());
  constructor() {
    const themeChanged = () => this.render();
    const motionChanged = () => this.systemReduced.set(this.motion.matches);
    this.dark.addEventListener('change', themeChanged);
    this.motion.addEventListener('change', motionChanged);
    inject(DestroyRef).onDestroy(() => {
      this.dark.removeEventListener('change', themeChanged);
      this.motion.removeEventListener('change', motionChanged);
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
      value.theme === 'system' ? (this.dark.matches ? 'dark' : 'light') : value.theme;
    document.documentElement.dataset['reducedMotion'] = String(value.reducedMotion);
  }
}
