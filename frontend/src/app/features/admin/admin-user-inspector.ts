import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { ViewMotion } from '../../shared/ui/view-motion';
import { RouterLink } from '@angular/router';
import { Field } from '../../shared/ui/field';
import { safeMessage } from '../../core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import type {
  AdminConversationDetailDto,
  AdminConversationPageDto,
  AdminUserDetailDto,
  AdminUserDto,
  AdminUserModelPolicyDto,
  ModelDto,
} from '../../core/api/schema';
import { Icon } from '../../shared/ui/icon';
import { SearchField } from '../../shared/ui/search-field';
import { Checkbox } from '../../shared/ui/checkbox';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { formatBytes, formatDate, formatNumber } from '../../shared/browser/format';
import { AdminApi } from './admin-api';
import { StorageUsage } from '../files/storage-usage';
import { RunTimingDisplay } from '../workspace/run-timing';
import { parseStorageLimitGb, storageLimitGb } from '../../shared/browser/storage-limit';
import {
  ModelPolicyEditor,
  modelPolicyDraft,
  modelPolicyRequest,
  type ModelPolicyDraft,
} from './model-policy-editor';
import { WorkspaceSession } from '../../core/auth/workspace-session';

@Component({
  selector: 'nx-admin-user-inspector',
  imports: [
    Notice,
    EmptyState,
    ViewSwitch,
    CompactDialog,
    ViewMotion,
    RouterLink,
    Field,
    Icon,
    SearchField,
    Checkbox,
    MarkdownView,
    StorageUsage,
    RunTimingDisplay,
    ModelPolicyEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-user-inspector.html',
  styleUrls: [
    '../../../styles/admin-inspector.scss',
    '../../../styles/admin-inspector-reading.scss',
  ],
  encapsulation: ViewEncapsulation.None,
})
export class AdminUserInspector {
  readonly user = input<AdminUserDto | null>(null);
  readonly closed = output<void>();
  readonly settingsChanged = output<void>();
  readonly models = input<ModelDto[]>([]);
  readonly tab = signal('conversations');
  readonly modelPolicy = signal<AdminUserModelPolicyDto | null>(null);
  readonly modelDraft = signal<ModelPolicyDraft>(modelPolicyDraft());
  readonly savingModels = signal(false);
  readonly modelError = signal('');
  readonly modelNotice = signal('');
  readonly loadingModels = signal(false);
  readonly storageLimit = signal('');
  readonly savingStorage = signal(false);
  readonly storageError = signal('');
  readonly storageNotice = signal('');
  readonly session = inject(WorkspaceSession);
  readonly overview = signal<AdminUserDetailDto | null>(null);
  readonly conversations = signal<AdminConversationPageDto | null>(null);
  readonly detail = signal<AdminConversationDetailDto | null>(null);
  readonly search = signal('');
  readonly includeDeleted = signal(true);
  readonly loading = signal(false);
  readonly reading = signal(false);
  readonly error = signal('');
  readonly readError = signal('');
  readonly date = formatDate;
  readonly format = formatNumber;
  readonly bytes = formatBytes;
  private readonly api = inject(AdminApi);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private version = 0;
  private listVersion = 0;
  private readVersion = 0;
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    effect(() => {
      const user = this.user(),
        dialog = this.dialog().nativeElement;
      ++this.version;
      ++this.listVersion;
      ++this.readVersion;
      clearTimeout(this.timer);
      this.overview.set(null);
      this.conversations.set(null);
      this.detail.set(null);
      this.search.set('');
      this.includeDeleted.set(true);
      this.error.set('');
      this.readError.set('');
      this.reading.set(false);
      this.storageError.set('');
      this.storageNotice.set('');
      this.savingStorage.set(false);
      this.storageLimit.set('');
      this.tab.set('conversations');
      this.modelPolicy.set(null);
      this.modelDraft.set(modelPolicyDraft());
      this.modelError.set('');
      this.modelNotice.set('');
      this.loadingModels.set(false);
      this.savingModels.set(false);
      if (user) {
        if (!dialog.open) dialog.showModal();
        void this.load(user.id, this.version);
      } else if (dialog.open) dialog.close();
    });
    inject(DestroyRef).onDestroy(() => {
      ++this.version;
      ++this.listVersion;
      ++this.readVersion;
      clearTimeout(this.timer);
    });
  }
  close() {
    this.dialog().nativeElement.close();
  }
  private async load(id: string, version: number) {
    const listVersion = this.listVersion;
    this.loading.set(true);
    try {
      const [overview, conversations] = await Promise.all([
        this.api.insights(id),
        this.api.conversations(id, '', true),
      ]);
      if (version !== this.version) return;
      this.overview.set(overview);
      this.storageLimit.set(storageLimitGb(overview.user.storage?.personalLimitBytes));
      if (listVersion === this.listVersion) this.conversations.set(conversations);
    } catch (error) {
      if (version === this.version) this.error.set(this.message(error));
    } finally {
      if (version === this.version && listVersion === this.listVersion) this.loading.set(false);
    }
  }
  async saveStorage(event: Event) {
    event.preventDefault();
    const user = this.user(),
      version = this.version;
    if (!user || this.savingStorage()) return;
    this.storageError.set('');
    this.storageNotice.set('');
    this.savingStorage.set(true);
    try {
      const bytes = parseStorageLimitGb(this.storageLimit());
      await this.api.storage(user.id, bytes);
      const overview = await this.api.insights(user.id);
      if (version !== this.version) return;
      this.overview.set(overview);
      this.storageNotice.set('個人容量上限已更新。');
      this.settingsChanged.emit();
    } catch (error) {
      if (version === this.version) this.storageError.set(this.message(error));
    } finally {
      if (version === this.version) this.savingStorage.set(false);
    }
  }
  async selectTab(tab: string) {
    this.tab.set(tab);
    if (tab !== 'models' || this.modelPolicy() || this.loadingModels()) return;
    await this.loadModels();
  }
  async loadModels() {
    const user = this.user(),
      version = this.version;
    if (!user) return;
    this.loadingModels.set(true);
    this.modelError.set('');
    try {
      const policy = await this.api.modelPolicy(user.id);
      if (version !== this.version) return;
      this.modelPolicy.set(policy);
      this.modelDraft.set(modelPolicyDraft(policy.personal));
    } catch (error) {
      if (version === this.version) this.modelError.set(this.message(error));
    } finally {
      if (version === this.version) this.loadingModels.set(false);
    }
  }
  async saveModels(event: Event) {
    event.preventDefault();
    const user = this.user(),
      version = this.version;
    if (!user || this.savingModels()) return;
    this.savingModels.set(true);
    this.modelError.set('');
    this.modelNotice.set('');
    try {
      await this.api.saveModelPolicy(user.id, modelPolicyRequest(this.modelDraft()));
      const policy = await this.api.modelPolicy(user.id);
      if (version !== this.version) return;
      this.modelPolicy.set(policy);
      this.modelDraft.set(modelPolicyDraft(policy.personal));
      this.modelNotice.set('個人模型政策已儲存，下次生成生效。');
      this.settingsChanged.emit();
    } catch (error) {
      if (version === this.version) this.modelError.set(this.message(error));
    } finally {
      if (version === this.version) this.savingModels.set(false);
    }
  }
  searchChanged(value: string) {
    this.search.set(value);
    clearTimeout(this.timer);
    ++this.listVersion;
    this.timer = setTimeout(() => void this.loadList(), 250);
  }
  deletedChanged(value: boolean) {
    this.includeDeleted.set(value);
    clearTimeout(this.timer);
    void this.loadList();
  }
  async loadList(offset = 0) {
    const user = this.user();
    if (!user) return;
    const version = ++this.listVersion;
    this.error.set('');
    this.loading.set(true);
    try {
      const rows = await this.api.conversations(
        user.id,
        this.search(),
        this.includeDeleted(),
        offset,
      );
      if (version === this.listVersion) this.conversations.set(rows);
    } catch (error) {
      if (version === this.listVersion) this.error.set(this.message(error));
    } finally {
      if (version === this.listVersion) this.loading.set(false);
    }
  }
  async read(id: string, more = false) {
    if (this.reading() && more) return;
    const version = ++this.readVersion;
    const old = more ? this.detail() : null;
    if (!more) this.detail.set(null);
    this.reading.set(true);
    this.readError.set('');
    try {
      const detail = await this.api.conversation(id, old?.messages.length || 0);
      if (version === this.readVersion)
        this.detail.set(
          old ? { ...detail, offset: 0, messages: [...old.messages, ...detail.messages] } : detail,
        );
    } catch (error) {
      if (version === this.readVersion) this.readError.set(this.message(error));
    } finally {
      if (version === this.readVersion) this.reading.set(false);
    }
  }
  private message(error: unknown) {
    return safeMessage(error);
  }
}
