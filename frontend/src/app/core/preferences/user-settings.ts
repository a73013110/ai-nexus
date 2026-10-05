import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { ApiTransport } from '../api/api-transport';
import type { UserSettings, PersonalUsage, EffectiveModelPolicy } from '../api/types';
export type { UserSettings, PersonalUsage } from '../api/types';
import { ThemeService } from './theme-service';
import { AuthService } from '../auth/auth-service';

export const defaultSettings = (): UserSettings => ({
  appearance: { theme: 'system', reducedMotion: false, defaultModelId: null },
  readingFontSize: 17,
  readingLineHeight: 1.8,
  density: 'comfortable',
  sidebarWidth: 264,
  readingWidth: 'standard',
  enterToSend: true,
  autoFollow: true,
  saveLocalDrafts: true,
  notifyOnCompletion: false,
  defaultReasoningEffort: 'auto',
});

@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private readonly http = inject(ApiTransport);
  private readonly themes = inject(ThemeService);
  private readonly auth = inject(AuthService);
  readonly value = signal<UserSettings>(defaultSettings());
  private owner = '';
  private generation = -1;
  constructor() {
    effect(() => {
      const generation = this.auth.generation();
      if (this.generation >= 0 && generation !== this.generation)
        untracked(() => {
          this.owner = '';
          this.generation = -1;
          this.value.set(defaultSettings());
          this.render(this.value());
        });
    });
  }
  async load(owner: string, refresh = false) {
    if (!refresh && this.owner === owner && this.generation === this.auth.generation())
      return this.value();
    const generation = this.auth.generation();
    const value = await this.http.json<UserSettings>('/settings');
    if (generation !== this.auth.generation()) throw new Error('登入身分已變更，請重新載入。');
    this.owner = owner;
    this.generation = generation;
    this.apply(value);
    return value;
  }
  preview(value: UserSettings) {
    this.render(value);
    this.themes.apply(value.appearance);
  }
  restore() {
    this.preview(this.value());
  }
  async save(value: UserSettings) {
    const generation = this.auth.generation();
    const saved = await this.http.json<UserSettings>('/settings', 'PUT', value);
    if (generation !== this.auth.generation()) throw new Error('登入身分已變更，請重新載入。');
    this.apply(saved);
    return saved;
  }
  usage() {
    return this.http.json<PersonalUsage>('/settings/usage');
  }
  policy() {
    return this.http.json<EffectiveModelPolicy>('/settings/model-policy');
  }
  private apply(value: UserSettings) {
    this.value.set(value);
    this.preview(value);
  }
  private render(value: UserSettings) {
    const root = document.documentElement;
    root.style.setProperty('--text-body', `${value.readingFontSize / 16}rem`);
    root.style.setProperty('--line-reading', String(value.readingLineHeight));
    root.style.setProperty(
      '--sidebar-width',
      `min(${value.sidebarWidth / 16}rem, calc(100vw - var(--button-target)))`,
    );
    root.style.setProperty(
      '--reading-width',
      value.readingWidth === 'narrow' ? '44rem' : value.readingWidth === 'wide' ? '64rem' : '52rem',
    );
    root.style.setProperty('--message-gap', value.density === 'compact' ? '1rem' : '1.5rem');
    root.dataset['density'] = value.density;
  }
  notifyCompleted() {
    if (
      this.value().notifyOnCompletion &&
      document.visibilityState === 'hidden' &&
      'Notification' in window &&
      Notification.permission === 'granted'
    )
      new Notification('AI Nexus', {
        body: 'AI 回覆已完成，回到工作區查看。',
        tag: 'nexus-completed',
      });
  }
}
