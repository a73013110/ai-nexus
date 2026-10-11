import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { Field } from '../../shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
  linkedSignal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminApi } from './admin-api';
import type {
  CollectionDto,
  EmbeddingProfileDto,
  KnowledgeSearchDto,
  RetrievalCapabilitiesDto,
} from '../../core/api/schema';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate } from '../../shared/browser/format';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Select } from '../../shared/ui/select';
import { Checkbox } from '../../shared/ui/checkbox';
import { JobProgress } from '../tasks/job-progress';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { RetrievalResults } from '../knowledge/retrieval-results';

@Component({
  selector: 'nx-retrieval-admin',
  imports: [
    Notice,
    Card,
    Field,
    ReactiveFormsModule,
    Select,
    Checkbox,
    JobProgress,
    ConfirmDialog,
    RetrievalResults,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './retrieval-admin.html',
  styleUrl: './retrieval-admin.scss',
})
export class RetrievalAdmin {
  private readonly api = inject(AdminApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly scope = inject(ViewScope);
  readonly session = inject(WorkspaceSession);
  private readonly profilesRead = apiResource({
    feature: 'admin',
    loader: () => this.api.embeddingProfiles(),
    poll: (profiles) =>
      profiles?.some((x) => x.job && ['queued', 'running'].includes(x.job.status)) ? 2000 : null,
  });
  private readonly capabilitiesRead = apiResource({
    feature: 'admin',
    loader: () => this.api.retrievalCapabilities(),
  });
  private readonly collectionsRead = apiResource({ loader: () => this.knowledge.collections() });
  readonly profiles = computed<EmbeddingProfileDto[]>(() => this.profilesRead.value() ?? []);
  readonly capabilities = computed<RetrievalCapabilitiesDto | null>(
    () => this.capabilitiesRead.value() ?? null,
  );
  readonly collections = computed<CollectionDto[]>(() => this.collectionsRead.value() ?? []);
  /** Chosen collections, pruned to those still readable after each read. */
  readonly selected = linkedSignal<CollectionDto[] | undefined, string[]>({
    source: this.collectionsRead.value,
    computation: (collections, previous) =>
      (previous?.value ?? []).filter(
        (id) => !collections || collections.some((x) => x.resource.id === id),
      ),
  });
  readonly result = signal<KnowledgeSearchDto | null>(null);
  readonly loading = computed(
    () =>
      this.profilesRead.loading() ||
      this.capabilitiesRead.loading() ||
      this.collectionsRead.loading(),
  );
  readonly working = signal(false);
  readonly searching = signal(false);
  readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.profilesRead.error() ||
      this.capabilitiesRead.error() ||
      this.collectionsRead.error(),
  );
  readonly notice = signal('');
  readonly confirmation = viewChild.required(ConfirmDialog);
  readonly form = new FormGroup({
    query: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(2000)],
    }),
  });
  readonly mode = signal('hybrid');
  readonly rerank = signal(false);
  readonly modes = [
    { value: 'hybrid', label: '混合檢索' },
    { value: 'vector', label: '向量檢索' },
    { value: 'keyword', label: '全文檢索' },
  ];
  readonly date = formatDate;
  readonly statuses: Record<string, string> = {
    active: '使用中',
    building: '重建中',
    retired: '已退役',
  };
  load() {
    this.actionError.set('');
    this.profilesRead.reload();
    this.capabilitiesRead.reload();
    this.collectionsRead.reload();
  }
  select(id: string, checked: boolean) {
    this.selected.update((ids) => (checked ? [...ids, id] : ids.filter((x) => x !== id)));
  }
  async operate(profile: EmbeddingProfileDto, action: 'rebuild' | 'activate' | 'clear') {
    if (this.working()) return;
    if (
      action === 'clear' &&
      !(await this.confirmation().ask({
        title: '清除退役向量',
        message: `清除 ${profile.model} 的退役向量。原始檔、逐頁文字與索引紀錄會保留。`,
        confirm: '清除向量',
        danger: true,
      }))
    )
      return;
    const guard = this.scope.guard();
    this.working.set(true);
    this.actionError.set('');
    this.notice.set('');
    try {
      if (action === 'clear') await this.api.clearProfileVectors(profile.id);
      else if (action === 'rebuild') await this.api.rebuildProfile(profile.id);
      else await this.api.activateProfile(profile.id);
      if (!guard()) return;
      this.notice.set(
        action === 'rebuild'
          ? '重建工作已排入佇列，可從進度查看結果。'
          : action === 'activate'
            ? '使用中索引已切換。'
            : '退役向量已清除。',
      );
      this.profilesRead.reload();
    } catch (error) {
      if (guard()) this.actionError.set(this.scope.message(error));
    } finally {
      if (guard()) this.working.set(false);
    }
  }
  async probe() {
    const guard = this.scope.guard();
    this.working.set(true);
    this.actionError.set('');
    try {
      const result = await this.api.probeRetrieval();
      if (guard()) this.capabilitiesRead.value.set(result);
    } catch (error) {
      if (guard()) this.actionError.set(this.scope.message(error));
    } finally {
      if (guard()) this.working.set(false);
    }
  }
  async search(event: Event) {
    event.preventDefault();
    if (
      this.form.invalid ||
      !this.form.controls.query.value.trim() ||
      !this.selected().length ||
      this.searching()
    )
      return;
    const guard = this.scope.guard();
    this.searching.set(true);
    this.actionError.set('');
    this.result.set(null);
    try {
      const result = await this.api.searchRetrieval({
        query: this.form.controls.query.value,
        collectionIds: this.selected(),
        mode: this.mode(),
        rerank: this.rerank(),
      });
      if (guard()) this.result.set(result);
    } catch (error) {
      if (guard()) this.actionError.set(this.scope.message(error));
    } finally {
      if (guard()) this.searching.set(false);
    }
  }
}
