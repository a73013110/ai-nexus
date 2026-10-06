import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
  effect,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import {
  PersonalUsage,
  UserSettings,
  UserSettingsService,
  defaultSettings,
} from '../../core/preferences/user-settings';
import { DraftRepository } from '../../core/preferences/draft-repository';
import { NexusApi } from '../../core/api/nexus-api';
import type { Models, EffectiveModelPolicy } from '../../core/api/types';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { WorkspaceNavigation } from '../../shared/ui/workspace-navigation';
import { downloadFile } from '../../shared/browser/download';
import { TokenUsageChart } from '../../shared/ui/token-usage-chart';
import { StorageUsage } from '../../shared/ui/storage-usage';
import {
  formatDuration,
  formatBytes,
  formatModelName,
  formatModelDisplayName,
} from '../../shared/browser/format';

@Component({
  selector: 'nx-settings-page',
  imports: [RouterLink, Select, Icon, WorkspaceNavigation, StorageUsage, TokenUsageChart],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  readonly embedded = input(false);
  readonly closeRequested = output<void>();
  readonly stateChange = output<{ changed: boolean; saving: boolean }>();
  readonly session = inject(WorkspaceSession);
  readonly service = inject(UserSettingsService);
  private readonly api = inject(NexusApi);
  private readonly drafts = inject(DraftRepository);
  readonly draft = signal<UserSettings>(defaultSettings());
  readonly saved = signal<UserSettings>(defaultSettings());
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly search = signal('');
  readonly section = signal('appearance');
  readonly usage = signal<PersonalUsage | null>(null);
  readonly models = signal<Models | null>(null);
  readonly policy = signal<EffectiveModelPolicy | null>(null);
  readonly notificationPermission = signal(
    'Notification' in window ? Notification.permission : 'unsupported',
  );
  readonly modelLabel = formatModelDisplayName;
  readonly sections = [
    {
      id: 'appearance',
      name: '外觀與閱讀',
      icon: 'sliders',
      keywords: '字級 字體 行距 主題 密度 側欄 寬度 動畫',
    },
    { id: 'conversation', name: '對話', icon: 'lines', keywords: '模型 思考 強度 送出 自動捲動' },
    { id: 'notifications', name: '通知', icon: 'info', keywords: '完成 回覆 瀏覽器' },
    { id: 'data', name: '資料與草稿', icon: 'archive', keywords: '保存 清除 匯出 備份' },
    { id: 'account', name: '帳號與權限', icon: 'lock', keywords: 'AD 登入 角色 功能 登出' },
    { id: 'usage', name: '使用狀況', icon: 'idea', keywords: '用量 tokens 檔案 限制 統計' },
    { id: 'shortcuts', name: '鍵盤快捷鍵', icon: 'command', keywords: '操作 快速 指令' },
  ];
  readonly visibleSections = computed(() =>
    this.sections.filter((x) => `${x.name} ${x.keywords}`.includes(this.search().trim())),
  );
  readonly currentSection = computed(() => this.sections.find((x) => x.id === this.section())!);
  readonly changed = computed(() => JSON.stringify(this.draft()) !== JSON.stringify(this.saved()));
  readonly themeOptions = [
    { value: 'system', label: '跟隨系統' },
    { value: 'light', label: '淺色' },
    { value: 'dark', label: '深色' },
  ];
  readonly fontOptions = [12, 13, 14, 15, 16, 17, 18, 20, 22, 24].map((x) => ({
    value: String(x),
    label: `${x} px`,
  }));
  readonly lineOptions = [1, 1.2, 1.4, 1.5, 1.6, 1.8, 2, 2.2].map((x) => ({
    value: String(x),
    label: `${x} 倍`,
  }));
  readonly densityOptions = [
    { value: 'comfortable', label: '舒適', description: '適合長文閱讀' },
    { value: 'compact', label: '緊湊', description: '一次看見更多內容' },
  ];
  readonly widthOptions = [
    { value: 'narrow', label: '專注 · 窄' },
    { value: 'standard', label: '標準' },
    { value: 'wide', label: '寬廣' },
  ];
  readonly sidebarOptions = [240, 264, 288, 320, 360].map((x) => ({
    value: String(x),
    label: `${x} px`,
  }));
  readonly effortOptions = [
    { value: 'auto', label: '依模型預設' },
    { value: 'minimal', label: '快速回應' },
    { value: 'low', label: '輕度思考' },
    { value: 'medium', label: '標準思考' },
    { value: 'high', label: '深入思考' },
  ];
  readonly modelOptions = computed(() => [
    { value: '', label: '依系統預設' },
    ...(this.models()?.models || []).map((x) => ({ value: x.id, label: formatModelName(x) })),
  ]);
  private alive = true;
  constructor() {
    effect(() => this.stateChange.emit({ changed: this.changed(), saving: this.saving() }));
    void this.load();
    inject(DestroyRef).onDestroy(() => {
      this.alive = false;
      this.service.restore();
    });
  }
  async load() {
    this.loading.set(true);
    this.error.set('');
    try {
      const me = await this.session.load(true);
      if (!me || !this.alive) return;
      const value = await this.service.load(me.id, true);
      if (!this.alive) return;
      this.draft.set(structuredClone(value));
      this.saved.set(structuredClone(value));
      const results = await Promise.allSettled([
        this.service.usage(),
        this.session.has('chat') ? this.api.models() : Promise.resolve(null),
        this.service.policy(),
      ]);
      if (!this.alive) return;
      if (results[0].status === 'fulfilled') this.usage.set(results[0].value);
      else this.error.set('使用統計暫時無法取得，其他偏好仍可設定。');
      if (results[1].status === 'fulfilled') this.models.set(results[1].value);
      if (results[2].status === 'fulfilled') this.policy.set(results[2].value);
    } catch (error) {
      if (this.alive)
        this.error.set(error instanceof Error ? error.message : '設定載入失敗，請重試。');
    } finally {
      if (this.alive) this.loading.set(false);
    }
  }
  update<K extends keyof UserSettings>(key: K, value: UserSettings[K]) {
    this.draft.update((current) => ({ ...current, [key]: value }));
    this.service.preview(this.draft());
    this.notice.set('');
  }
  appearance(key: 'theme' | 'defaultModelId' | 'reducedMotion', value: string | boolean | null) {
    this.draft.update((current) => ({
      ...current,
      appearance: { ...current.appearance, [key]: value },
    }));
    this.service.preview(this.draft());
    this.notice.set('');
  }
  searchChanged(value: string) {
    this.search.set(value);
    if (!this.visibleSections().some((x) => x.id === this.section()) && this.visibleSections()[0])
      this.section.set(this.visibleSections()[0].id);
  }
  reset() {
    this.draft.set(structuredClone(this.saved()));
    this.service.restore();
    this.notice.set('已取消尚未儲存的變更。');
  }
  async save() {
    this.saving.set(true);
    this.error.set('');
    try {
      const value = await this.service.save(this.draft());
      if (!this.alive) return;
      this.draft.set(structuredClone(value));
      this.saved.set(structuredClone(value));
      this.notice.set('已儲存，會套用於此帳號的其他裝置。');
    } catch (error) {
      if (this.alive) this.error.set(error instanceof Error ? error.message : '未儲存，請重試。');
    } finally {
      if (this.alive) this.saving.set(false);
    }
  }
  async enableNotifications() {
    if (!('Notification' in window)) return;
    const permission = await Notification.requestPermission();
    if (!this.alive) return;
    this.notificationPermission.set(permission);
    if (permission === 'granted') this.update('notifyOnCompletion', true);
  }
  clearDrafts() {
    const owner = this.session.me()?.id;
    if (!owner) return;
    this.notice.set(
      this.drafts.clearAccount(owner)
        ? '已清除這個瀏覽器中此帳號的草稿。'
        : '瀏覽器阻擋儲存空間存取，草稿未清除。',
    );
  }
  exportPreferences() {
    downloadFile(
      JSON.stringify({ version: 1, settings: this.saved() }, null, 2),
      'ai-nexus-settings',
      'json',
    );
  }
  readonly number = Number;
  format(value: number) {
    return value.toLocaleString('zh-TW');
  }
  readonly duration = formatDuration;
  readonly bytes = formatBytes;
}
