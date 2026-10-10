import { ViewScope } from '../../shared/browser/view-scope';
import { apiResource } from '../../core/api/api-resource';
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
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
  linkedSignal,
  untracked,
  computed,
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
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-user-inspector.html',
  styleUrls: ['./admin-user-inspector.scss', './admin-user-inspector-reading.scss'],
})
export class AdminUserInspector {
  readonly user = input<AdminUserDto | null>(null);
  readonly closed = output<void>();
  readonly settingsChanged = output<void>();
  readonly models = input<ModelDto[]>([]);
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AdminApi);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly userId = computed(() => this.user()?.id);
  readonly tab = linkedSignal(() => (this.userId(), 'conversations'));
  private readonly overviewRead = apiResource({
    params: () => this.userId(),
    loader: async (id) => ({ id, data: await this.api.insights(id) }),
  });
  readonly overview = computed<AdminUserDetailDto | null>(() => {
    const loaded = this.overviewRead.value();
    return loaded && loaded.id === this.userId() ? loaded.data : null;
  });
  readonly storageLimit = linkedSignal(() =>
    storageLimitGb(this.overview()?.user.storage?.personalLimitBytes),
  );
  readonly savingStorage = signal(false);
  readonly storageError = signal('');
  readonly storageNotice = signal('');
  /** The model policy is read the first time its tab is opened for this user. */
  private readonly modelsWanted = linkedSignal(() => (this.userId(), false));
  private readonly modelsRead = apiResource({
    params: () => (this.modelsWanted() ? this.userId() : undefined),
    loader: async (id) => ({ id, data: await this.api.modelPolicy(id) }),
  });
  readonly modelPolicy = computed<AdminUserModelPolicyDto | null>(() => {
    const loaded = this.modelsRead.value();
    return loaded && loaded.id === this.userId() ? loaded.data : null;
  });
  readonly modelDraft = linkedSignal<ModelPolicyDraft>(() =>
    modelPolicyDraft(this.modelPolicy()?.personal),
  );
  readonly loadingModels = this.modelsRead.loading;
  readonly savingModels = signal(false);
  readonly saveModelError = signal('');
  readonly modelError = computed(() => this.saveModelError() || this.modelsRead.error());
  readonly modelNotice = signal('');
  readonly search = linkedSignal(() => (this.userId(), ''));
  private readonly query = linkedSignal(() => (this.userId(), ''));
  private readonly typing = signal(false);
  readonly includeDeleted = linkedSignal(() => (this.userId(), true));
  private readonly offset = linkedSignal(() => (this.userId(), 0));
  private readonly listRead = apiResource({
    params: () => {
      const id = this.userId();
      return id
        ? { id, search: this.query(), includeDeleted: this.includeDeleted(), offset: this.offset() }
        : undefined;
    },
    loader: async (request) => ({
      id: request.id,
      data: await this.api.conversations(
        request.id,
        request.search,
        request.includeDeleted,
        request.offset,
      ),
    }),
  });
  readonly conversations = computed<AdminConversationPageDto | null>(() => {
    const loaded = this.listRead.value();
    return loaded && loaded.id === this.userId() ? loaded.data : null;
  });
  readonly loading = computed(() => this.typing() || this.listRead.refreshing());
  readonly error = computed(() => this.overviewRead.error() || this.listRead.error());
  /** The conversation being read and how many of its messages are already shown. */
  private readonly opened = linkedSignal<string | undefined, { id: string; offset: number } | null>(
    { source: this.userId, computation: () => null },
  );
  private readonly detailRead = apiResource({
    params: () => this.opened() ?? undefined,
    loader: async (request) => ({
      request,
      data: await this.api.conversation(request.id, request.offset),
    }),
  });
  readonly detail = linkedSignal<
    {
      request: { id: string; offset: number } | null;
      loaded?: { request: { id: string; offset: number }; data: AdminConversationDetailDto };
    },
    AdminConversationDetailDto | null
  >({
    source: () => ({ request: this.opened(), loaded: this.detailRead.value() }),
    computation: ({ request, loaded }, previous) => {
      if (!request) return null;
      const shown = request.offset ? (previous?.value ?? null) : null;
      if (loaded?.request !== request) return shown;
      return shown
        ? { ...loaded.data, offset: 0, messages: [...shown.messages, ...loaded.data.messages] }
        : loaded.data;
    },
  });
  readonly reading = this.detailRead.refreshing;
  readonly readError = this.detailRead.error;
  readonly date = formatDate;
  readonly format = formatNumber;
  readonly bytes = formatBytes;
  constructor() {
    effect(() => {
      const user = this.user(),
        dialog = this.dialog().nativeElement;
      untracked(() => {
        this.scope.cancel('inspector-search');
        this.typing.set(false);
        this.storageError.set('');
        this.storageNotice.set('');
        this.savingStorage.set(false);
        this.saveModelError.set('');
        this.modelNotice.set('');
        this.savingModels.set(false);
      });
      if (user) {
        if (!dialog.open) dialog.showModal();
      } else if (dialog.open) dialog.close();
    });
  }
  close() {
    this.dialog().nativeElement.close();
  }
  async saveStorage(event: Event) {
    event.preventDefault();
    const user = this.user(),
      alive = this.scope.guard(),
      valid = () => alive() && this.userId() === user?.id;
    if (!user || this.savingStorage()) return;
    this.storageError.set('');
    this.storageNotice.set('');
    this.savingStorage.set(true);
    try {
      const bytes = parseStorageLimitGb(this.storageLimit());
      await this.api.storage(user.id, bytes);
      if (!valid()) return;
      this.overviewRead.reload();
      this.storageNotice.set('個人容量上限已更新。');
      this.settingsChanged.emit();
    } catch (error) {
      if (valid()) this.storageError.set(safeMessage(error));
    } finally {
      if (valid()) this.savingStorage.set(false);
    }
  }
  selectTab(tab: string) {
    this.tab.set(tab);
    if (tab === 'models') this.modelsWanted.set(true);
  }
  loadModels() {
    this.saveModelError.set('');
    this.modelsRead.reload();
  }
  async saveModels(event: Event) {
    event.preventDefault();
    const user = this.user(),
      alive = this.scope.guard(),
      valid = () => alive() && this.userId() === user?.id;
    if (!user || this.savingModels()) return;
    this.savingModels.set(true);
    this.saveModelError.set('');
    this.modelNotice.set('');
    try {
      await this.api.saveModelPolicy(user.id, modelPolicyRequest(this.modelDraft()));
      if (!valid()) return;
      this.modelsRead.reload();
      this.modelNotice.set('個人模型政策已儲存，下次生成生效。');
      this.settingsChanged.emit();
    } catch (error) {
      if (valid()) this.saveModelError.set(safeMessage(error));
    } finally {
      if (valid()) this.savingModels.set(false);
    }
  }
  searchChanged(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.offset.set(0);
        this.query.set(value);
      },
      250,
      'inspector-search',
    );
  }
  deletedChanged(value: boolean) {
    this.typing.set(false);
    this.scope.cancel('inspector-search');
    this.query.set(this.search());
    this.offset.set(0);
    this.includeDeleted.set(value);
  }
  loadList(offset = 0) {
    this.offset.set(offset);
  }
  read(id: string, more = false) {
    if (more) {
      if (this.reading()) return;
      this.opened.set({ id, offset: this.detail()?.messages.length || 0 });
    } else this.opened.set({ id, offset: 0 });
  }
}
