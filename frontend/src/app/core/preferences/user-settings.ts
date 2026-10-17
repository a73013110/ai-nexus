import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { ApiClient } from '../api/api-client';
import type { UserSettingsDto } from '../api/schema';
import { ThemeService } from './theme-service';
import { AuthService } from '../auth/auth-service';

export const defaultSettings = (): UserSettingsDto => ({
  appearance: { theme: 'system', reducedMotion: false, defaultModelId: null },
  readingFontSize: 15,
  readingLineHeight: 1.2,
  density: 'comfortable',
  sidebarWidth: 240,
  readingWidth: 'standard',
  enterToSend: true,
  autoFollow: true,
  saveLocalDrafts: true,
  notifyOnCompletion: false,
  defaultReasoningEffort: 'auto',
});

@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private readonly api = inject(ApiClient);
  private readonly themes = inject(ThemeService);
  private readonly auth = inject(AuthService);
  readonly value = signal<UserSettingsDto>(defaultSettings());
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
  /** `owner` 可能仍在載入，因此帳號與其設定一起取得。 */
  async load(owner: string | Promise<string>, refresh = false) {
    // 同一次登入世代內，owner 不會改變。
    if (!refresh && this.owner && this.generation === this.auth.generation()) {
      if ((await owner) === this.owner) return this.value();
    }
    const current = this.auth.guard();
    const [id, value] = await Promise.all([owner, this.api.get('/api/v1/settings')]);
    if (!current()) throw new DOMException('Identity changed', 'AbortError');
    this.owner = id;
    this.generation = this.auth.generation();
    this.apply(value);
    return value;
  }
  preview(value: UserSettingsDto) {
    this.render(value);
    this.themes.apply(value.appearance);
  }
  restore() {
    this.preview(this.value());
  }
  async save(value: UserSettingsDto) {
    const current = this.auth.guard();
    const saved = await this.api.put('/api/v1/settings', { body: value });
    if (!current()) throw new DOMException('Identity changed', 'AbortError');
    this.apply(saved);
    return saved;
  }
  usage() {
    return this.api.get('/api/v1/settings/usage');
  }
  policy() {
    return this.api.get('/api/v1/settings/model-policy');
  }
  private apply(value: UserSettingsDto) {
    this.value.set(value);
    this.preview(value);
  }
  private render(value: UserSettingsDto) {
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
    root.style.setProperty('--message-gap', value.density === 'compact' ? '0.75rem' : '1rem');
    root.dataset['density'] = value.density;
  }
}
