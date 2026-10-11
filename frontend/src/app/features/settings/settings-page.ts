import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { ViewMotion } from '../../shared/ui/view-motion';
import { SearchField } from '../../shared/ui/search-field';
import { safeMessage } from '../../core/errors/safe-errors';
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
  linkedSignal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type {
  EffectiveModelPolicyDto,
  ModelsDto,
  PersonalUsageDto,
  UserSettingsDto,
} from '../../core/api/schema';
import { UserSettingsService, defaultSettings } from '../../core/preferences/user-settings';
import { DraftRepository } from '../../core/preferences/draft-repository';
import { NexusApi } from '../../core/api/nexus-api';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { WorkspaceNavigation } from '../../core/layout/workspace-navigation';
import { downloadFile } from '../../shared/browser/download';
import { TokenUsageChart } from '../billing/token-usage-chart';
import { StorageUsage } from '../files/storage-usage';
import {
  formatDuration,
  formatBytes,
  formatModelName,
  formatModelDisplayName,
} from '../../shared/browser/format';

@Component({
  selector: 'nx-settings-page',
  styleUrls: ['./settings-page.scss', './settings-content.scss', './settings-page-dialog.scss'],
  imports: [
    Notice,
    ViewMotion,
    Card,
    SearchField,
    RouterLink,
    Select,
    Icon,
    WorkspaceNavigation,
    StorageUsage,
    TokenUsageChart,
  ],
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
  /** The settings are read fresh with the account, never from another device's cache. */
  private readonly settingsRead = apiResource({
    freshAccount: true,
    loader: () => this.service.load(this.session.me()!.id, true),
  });
  private readonly usageRead = apiResource({ loader: () => this.service.usage() });
  private readonly modelsRead = apiResource({
    loader: () => (this.session.has('chat') ? this.api.models() : Promise.resolve(null)),
  });
  private readonly policyRead = apiResource({ loader: () => this.service.policy() });
  /** What the server holds; the draft starts from it and is compared against it. */
  readonly saved = linkedSignal<UserSettingsDto>(() =>
    structuredClone(this.settingsRead.value() ?? defaultSettings()),
  );
  readonly draft = linkedSignal<UserSettingsDto>(() => structuredClone(this.saved()));
  readonly loading = this.settingsRead.loading;
  readonly saving = signal(false);
  readonly saveError = signal('');
  readonly error = computed(
    () =>
      this.saveError() ||
      this.settingsRead.error() ||
      (this.usageRead.error() ? '使用統計暫時無法取得，其他偏好仍可設定。' : ''),
  );
  readonly notice = signal('');
  readonly search = signal('');
  readonly section = signal('appearance');
  readonly usage = computed<PersonalUsageDto | null>(() => this.usageRead.value() ?? null);
  readonly models = computed<ModelsDto | null>(() => this.modelsRead.value() ?? null);
  readonly policy = computed<EffectiveModelPolicyDto | null>(() => this.policyRead.value() ?? null);
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
    inject(DestroyRef).onDestroy(() => {
      this.alive = false;
      this.service.restore();
    });
  }
  load() {
    this.saveError.set('');
    this.settingsRead.reload();
    this.usageRead.reload();
    this.modelsRead.reload();
    this.policyRead.reload();
  }
  update<K extends keyof UserSettingsDto>(key: K, value: UserSettingsDto[K]) {
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
    this.saveError.set('');
    try {
      const value = await this.service.save(this.draft());
      if (!this.alive) return;
      this.saved.set(structuredClone(value));
      this.notice.set('已儲存，會套用於此帳號的其他裝置。');
    } catch (error) {
      if (this.alive) this.saveError.set(safeMessage(error));
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
