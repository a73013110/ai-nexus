import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type {
  CollectionDto,
  EmbeddingProfileDto,
  KnowledgeSearchDto,
  RetrievalCapabilitiesDto,
  RetrievalConnectionDto,
} from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { formatDate, formatDay, formatNumber, formatPercent } from '@shared/browser/format';
import { Card } from '@shared/ui/card';
import { Checkbox } from '@shared/ui/checkbox';
import { ConfirmDialog, type Confirmation } from '@shared/ui/confirm-dialog';
import type { BadgeTone } from '@shared/ui/count-badge';
import { DataTable, TableState } from '@shared/ui/data-table';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { ToastService } from '@shared/ui/toast-service';
import { RetrievalResults } from './retrieval-results';
import { KnowledgeApi } from '../knowledge-api';
import { JobProgress } from '@features/work/jobs/job-progress';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { AdministrationApi } from '@features/admin/access-control/administration-api';

type ProfileAction = 'rebuild' | 'activate' | 'clear';
interface Capability {
  key: string;
  name: string;
  tone: BadgeTone;
  status: string;
  detail: string;
  endpoint: string;
}
const statuses: Record<string, { label: string; tone: BadgeTone }> = {
  active: { label: '使用中', tone: 'success' },
  building: { label: '建立中', tone: 'info' },
  retired: { label: '已退役', tone: 'neutral' },
};
/** 一次測試最多能搜尋的集合數。 */
const BENCH_COLLECTIONS = 3;

/**
 * 檢索管理：嵌入索引與其覆蓋率和生命週期動作、檢索所依賴的服務，
 * 以及用作用中索引搜尋的測試台。
 */
@Component({
  selector: 'nx-retrieval-admin',
  imports: [
    Card,
    Checkbox,
    ConfirmDialog,
    DataTable,
    Field,
    Icon,
    JobProgress,
    Notice,
    ReactiveFormsModule,
    RetrievalResults,
    Select,
    StatusBadge,
    TableState,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './retrieval-admin.html',
  styleUrls: ['../../../admin/operations/operations-tabs.scss', './retrieval-admin.scss'],
})
export class RetrievalAdmin {
  private readonly api = inject(AdministrationApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  private readonly jobToasts = inject(JobToasts);
  readonly session = inject(WorkspaceSession);
  readonly profilesRead = apiResource({
    feature: 'operations',
    loader: () => this.api.embeddingProfiles(),
    poll: (profiles) =>
      profiles?.some((item) => item.job && ['queued', 'running'].includes(item.job.status))
        ? 2000
        : null,
  });
  readonly capabilitiesRead = apiResource({
    feature: 'operations',
    loader: () => this.api.retrievalCapabilities(),
  });
  private readonly collectionsRead = apiResource({ loader: () => this.knowledge.collections() });
  readonly profiles = computed<EmbeddingProfileDto[]>(() => this.profilesRead.value() ?? []);
  readonly collections = computed<CollectionDto[]>(() => this.collectionsRead.value() ?? []);
  readonly capabilities = computed(() => capabilityRows(this.capabilitiesRead.value() ?? null));
  /** 已選的集合，每次讀取後會修剪為仍可讀取的集合。 */
  readonly selected = linkedSignal<CollectionDto[] | undefined, string[]>({
    source: this.collectionsRead.value,
    computation: (collections, previous) =>
      (previous?.value ?? []).filter(
        (id) => !collections || collections.some((item) => item.resource.id === id),
      ),
  });
  readonly limit = BENCH_COLLECTIONS;
  readonly result = signal<KnowledgeSearchDto | null>(null);
  readonly working = signal(false);
  readonly probing = signal(false);
  readonly searching = signal(false);
  readonly actionError = signal('');
  /** 表格沒有資料時讀取失敗由表格狀態顯示（附重試）；已有資料的重新整理失敗才放到頁首提示。 */
  readonly error = computed(
    () =>
      this.actionError() ||
      (this.profiles().length ? this.profilesRead.error() : '') ||
      (this.capabilities().length ? this.capabilitiesRead.error() : '') ||
      this.collectionsRead.error(),
  );
  private readonly confirmation = viewChild.required(ConfirmDialog);
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
  readonly format = formatNumber;
  readonly date = formatDate;
  readonly day = formatDay;
  status(profile: EmbeddingProfileDto) {
    return statuses[profile.status] ?? { label: profile.status, tone: 'neutral' as BadgeTone };
  }
  coverage(profile: EmbeddingProfileDto) {
    return formatPercent(profile.coverage.ratio);
  }
  jobActive(profile: EmbeddingProfileDto) {
    return !!profile.job && ['queued', 'running'].includes(profile.job.status);
  }
  load() {
    this.actionError.set('');
    this.profilesRead.reload();
    this.capabilitiesRead.reload();
    this.collectionsRead.reload();
  }
  select(id: string, checked: boolean) {
    this.selected.update((ids) => (checked ? [...ids, id] : ids.filter((item) => item !== id)));
  }
  async operate(profile: EmbeddingProfileDto, action: ProfileAction) {
    if (this.working() || !(await this.confirmation().ask(confirmations[action](profile)))) return;
    const guard = this.scope.guard();
    this.working.set(true);
    this.actionError.set('');
    try {
      if (action === 'clear') await this.api.clearProfileVectors(profile.id);
      else if (action === 'rebuild')
        this.jobToasts.track(await this.api.rebuildProfile(profile.id));
      else await this.api.activateProfile(profile.id);
      if (!guard()) return;
      if (action === 'rebuild') this.toast.success('重建工作已排入佇列', '進度顯示在索引列。');
      else this.toast.success(action === 'activate' ? '使用中索引已切換' : '退役向量已清除');
      this.profilesRead.reload();
    } catch (error) {
      if (guard()) this.actionError.set(this.scope.message(error));
    } finally {
      if (guard()) this.working.set(false);
    }
  }
  async probe() {
    if (this.probing()) return;
    const guard = this.scope.guard();
    this.probing.set(true);
    this.actionError.set('');
    try {
      const result = await this.api.probeRetrieval();
      if (guard()) this.capabilitiesRead.value.set(result);
    } catch (error) {
      if (guard()) this.actionError.set(this.scope.message(error));
    } finally {
      if (guard()) this.probing.set(false);
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

const confirmations: Record<ProfileAction, (profile: EmbeddingProfileDto) => Confirmation> = {
  rebuild: (profile) => ({
    title: '重建檢索索引',
    message: `在背景為 ${profile.model} 重新向量化知識片段，會呼叫向量模型並計入用量；進度顯示在索引列。`,
    confirm: '開始重建',
  }),
  activate: (profile) => ({
    title: '切換使用中索引',
    message: `之後的知識檢索改用 ${profile.model}；目前使用中的索引會退役，保留到清除向量為止。`,
    confirm: '啟用索引',
  }),
  clear: (profile) => ({
    title: '清除退役向量',
    message: `清除 ${profile.model} 的退役向量。原始檔、逐頁文字與索引紀錄會保留。`,
    confirm: '清除向量',
    danger: true,
  }),
};

/** 檢索所依賴的項目，每項一列，附上徽章可顯示的狀態。 */
function capabilityRows(value: RetrievalCapabilitiesDto | null): Capability[] {
  if (!value) return [];
  const sql = value.sql;
  const sqlFeature = (key: string, name: string, ok: boolean | undefined, missing: string) => ({
    key,
    name,
    tone: (sql ? (ok ? 'success' : 'warning') : 'neutral') as BadgeTone,
    status: sql ? (ok ? '可用' : missing) : '無法確認',
    detail: '',
    endpoint: '',
  });
  const connection = (key: string, name: string, item2: RetrievalConnectionDto): Capability => ({
    key,
    name: `${name} ${item2.model}`.trim(),
    tone: item2.available === null ? 'neutral' : item2.available ? 'success' : 'danger',
    status: item2.available === null ? '未驗證' : item2.available ? '可用' : '無法使用',
    detail: item2.notice,
    endpoint: item2.endpoint,
  });
  return [
    {
      key: 'vector',
      name: 'SQL 原生向量',
      tone: sql?.nativeVector ? 'success' : 'danger',
      status: sql?.nativeVector ? '可用' : '無法使用',
      detail: sql ? `SQL Server ${sql.version} ${sql.edition}`.trim() : '',
      endpoint: '',
    },
    {
      ...sqlFeature('full-text', '全文檢索', sql?.fullTextInstalled, '未安裝'),
      detail: '關鍵字召回需要 SQL Server 全文檢索元件',
    },
    {
      ...sqlFeature('word-breaker', '繁體中文斷詞器', sql?.traditionalChineseWordBreaker, '缺少'),
      detail: 'LCID 1028',
    },
    {
      ...sqlFeature('full-text-index', '全文索引', sql?.fullTextIndex, '未建立'),
      detail: '知識片段的全文索引',
    },
    connection('embedding', '向量模型', value.embedding),
    connection('rerank', '重排模型', value.rerank),
  ];
}
