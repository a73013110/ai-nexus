import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  EmbeddingProfile,
  RetrievalCapabilities,
  KnowledgeSearch,
  Collection,
  Job,
} from '../../core/api/types';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate } from '../../shared/browser/format';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Select } from '../../shared/ui/select';
import { Checkbox } from '../../shared/ui/checkbox';
import { JobProgress } from '../../shared/ui/job-progress';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { RetrievalResults } from '../../shared/ui/retrieval-results';

@Component({
  selector: 'nx-retrieval-admin',
  imports: [IssueCode,ReactiveFormsModule, Select, Checkbox, JobProgress, ConfirmDialog, RetrievalResults],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './retrieval-admin.html',
  styleUrl: './retrieval-admin.scss',
})
export class RetrievalAdmin {
  private readonly api = inject(ApiTransport);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly scope = inject(ViewScope);
  readonly session = inject(WorkspaceSession);
  readonly profiles = signal<EmbeddingProfile[]>([]);
  readonly capabilities = signal<RetrievalCapabilities | null>(null);
  readonly collections = signal<Collection[]>([]);
  readonly selected = signal<string[]>([]);
  readonly result = signal<KnowledgeSearch | null>(null);
  readonly loading = signal(true);
  readonly working = signal(false);
  readonly searching = signal(false);
  readonly error = signal('');
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
  readonly activeJobs = computed(() =>
    this.profiles().some((x) => x.job && ['queued', 'running'].includes(x.job.status)),
  );
  readonly date = formatDate;
  readonly statuses: Record<string, string> = {
    active: '使用中',
    building: '重建中',
    retired: '已退役',
  };
  constructor() {
    void this.load();
  }
  async load() {
    const guard = this.scope.guard();
    this.error.set('');
    try {
      const [profiles, capabilities, collections] = await Promise.all([
        this.api.json<EmbeddingProfile[]>('/admin/knowledge/profiles'),
        this.api.json<RetrievalCapabilities>('/admin/knowledge/capabilities'),
        this.knowledge.collections(),
      ]);
      if (!guard()) return;
      this.profiles.set(profiles);
      this.capabilities.set(capabilities);
      this.collections.set(collections);
      this.selected.update((ids) =>
        ids.filter((id) => collections.some((x) => x.resource.id === id)),
      );
      if (this.activeJobs()) this.scope.later(() => void this.refreshProfiles(), 2000, 'profiles');
    } catch (error) {
      if (guard()) this.error.set(this.scope.message(error));
    } finally {
      if (guard()) this.loading.set(false);
    }
  }
  private async refreshProfiles() {
    const guard = this.scope.guard();
    try {
      const profiles = await this.api.json<EmbeddingProfile[]>('/admin/knowledge/profiles');
      if (!guard()) return;
      this.profiles.set(profiles);
      if (this.activeJobs()) this.scope.later(() => void this.refreshProfiles(), 2000, 'profiles');
    } catch (error) {
      if (guard()) this.error.set(this.scope.message(error));
    }
  }
  select(id: string, checked: boolean) {
    this.selected.update((ids) => (checked ? [...ids, id] : ids.filter((x) => x !== id)));
  }
  async operate(profile: EmbeddingProfile, action: 'rebuild' | 'activate' | 'clear') {
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
    this.error.set('');
    this.notice.set('');
    try {
      const route = `/admin/knowledge/profiles/${profile.id}`;
      if (action === 'clear') await this.api.json<void>(`${route}/vectors`, 'DELETE');
      else await this.api.json<Job | void>(`${route}/${action}`, 'POST');
      if (!guard()) return;
      this.notice.set(
        action === 'rebuild'
          ? '重建工作已排入佇列，可從進度查看結果。'
          : action === 'activate'
            ? '使用中索引已切換。'
            : '退役向量已清除。',
      );
      await this.refreshProfiles();
    } catch (error) {
      if (guard()) this.error.set(this.scope.message(error));
    } finally {
      if (guard()) this.working.set(false);
    }
  }
  async probe() {
    const guard = this.scope.guard();
    this.working.set(true);
    this.error.set('');
    try {
      const result = await this.api.json<RetrievalCapabilities>(
        '/admin/knowledge/capabilities/probe',
        'POST',
      );
      if (guard()) this.capabilities.set(result);
    } catch (error) {
      if (guard()) this.error.set(this.scope.message(error));
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
    this.error.set('');
    this.result.set(null);
    try {
      const result = await this.api.json<KnowledgeSearch>('/admin/knowledge/search', 'POST', {
        query: this.form.controls.query.value,
        collectionIds: this.selected(),
        mode: this.mode(),
        rerank: this.rerank(),
      });
      if (guard()) this.result.set(result);
    } catch (error) {
      if (guard()) this.error.set(this.scope.message(error));
    } finally {
      if (guard()) this.searching.set(false);
    }
  }
}
