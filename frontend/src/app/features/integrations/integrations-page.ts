import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { Card } from '@shared/ui/card';
import { EmptyState } from '@shared/ui/empty-state';
import { FilterPanel } from '@shared/ui/filter-panel';
import { Field } from '@shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  linkedSignal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ConversationDraftTransfer } from '@core/preferences/conversation-draft-transfer';
import type { SourceDetailDto, SourceDto, SourceRecordDto } from '@core/api/schema';
import { FeaturePage } from '@core/layout/feature-page';
import { Select } from '@shared/ui/select';
import { Icon } from '@shared/ui/icon';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { ViewScope } from '@shared/browser/view-scope';
import { formatDate } from '@shared/browser/format';
import { IntegrationsApi } from './integrations-api';

@Component({
  selector: 'nx-integrations-page',
  styleUrl: './integrations-page.scss',
  imports: [Notice, Card, EmptyState, FilterPanel, Field, FeaturePage, Select, Icon, MarkdownView],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './integrations-page.html',
})
export class IntegrationsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(IntegrationsApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transfer = inject(ConversationDraftTransfer);
  /** 開啟此頁時連結指定的來源與紀錄。 */
  private readonly linked = {
    source: this.route.snapshot.queryParamMap.get('source'),
    record: this.route.snapshot.queryParamMap.get('record'),
  };
  private readonly sourcesRead = apiResource({
    feature: 'integrations',
    loader: () => this.api.list(),
  });
  readonly sources = computed<SourceDto[]>(() => this.sourcesRead.value() ?? []);
  /** 連結指定的來源；沒有則為第一個，直到使用者選了別的。 */
  readonly current = linkedSignal<SourceDto[], SourceDto | null>({
    source: this.sources,
    computation: (sources, previous) =>
      sources.find((item) => item.id === (previous?.value?.id ?? this.linked.source)) ??
      sources[0] ??
      null,
  });
  readonly query = signal('');
  readonly kind = signal('all');
  /** 最後送出的搜尋；結果屬於它與它的來源。 */
  private readonly submitted = signal<{ source: string; query: string; kind: string } | null>(null);
  private readonly rowsRead = apiResource({
    feature: 'integrations',
    params: () => this.submitted() ?? undefined,
    loader: async (search) => ({
      search,
      rows: await this.api.search(search.source, search.query, search.kind),
    }),
  });
  private readonly results = computed(() => {
    const value = this.rowsRead.value();
    return value && value.search === this.submitted() ? value.rows : null;
  });
  readonly rows = computed<SourceRecordDto[]>(() => this.results() ?? []);
  readonly searched = computed(() => this.results() !== null);
  readonly searching = this.rowsRead.loading;
  /** 畫面上的紀錄：連結指定的、單一搜尋命中，或使用者開啟的那一筆。 */
  private readonly record = linkedSignal<SourceRecordDto[] | null, string | null>({
    source: this.results,
    computation: (rows, previous) =>
      !previous ? this.linked.record : rows?.length === 1 ? rows[0].id : null,
  });
  /** 分頁可見時，已開啟的紀錄每 30 秒重新檢查一次。 */
  private readonly detailRead = apiResource({
    feature: 'integrations',
    params: () => {
      const source = this.current(),
        id = this.record();
      return source?.canQuery && id ? { source: source.id, id } : undefined;
    },
    loader: (record) => this.api.get(record.source, record.id),
    poll: () => 30000,
  });
  readonly detail = computed<SourceDetailDto | null>(() => {
    const value = this.detailRead.value();
    return value && value.record.id === this.record() ? value : null;
  });
  readonly loading = this.sourcesRead.loading;
  readonly reading = this.detailRead.loading;
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.sourcesRead.error() ||
      this.rowsRead.error() ||
      this.detailRead.error(),
  );
  readonly kindOptions = computed(() => [
    { value: 'all', label: '全部類型' },
    ...(this.current()?.kinds ?? []).map((item) => ({ value: item, label: this.label(item) })),
  ]);
  private revision = 0;
  private guard() {
    const revision = this.revision,
      valid = this.scope.guard();
    return () => valid() && revision === this.revision;
  }
  select(source: SourceDto) {
    if (this.busy()) return;
    ++this.revision;
    this.current.set(source);
    this.submitted.set(null);
    this.record.set(null);
    this.query.set('');
    this.kind.set('all');
    this.actionError.set('');
  }
  search(event: Event) {
    event.preventDefault();
    const source = this.current();
    if (!source?.canQuery || this.searching() || this.query().trim().length < 2) return;
    this.actionError.set('');
    this.submitted.set({ source: source.id, query: this.query(), kind: this.kind() });
  }
  read(id: string) {
    if (this.busy() || !this.current()?.canQuery) return;
    this.actionError.set('');
    if (id === this.record()) this.detailRead.reload();
    else this.record.set(id);
  }
  async use(action: 'import' | 'chat') {
    const detail = this.detail(),
      source = this.current(),
      valid = this.guard();
    if (!detail || !source || this.busy()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      if (action === 'import') {
        const artifact = await this.api.import(source.id, detail.record.id, detail.record.revision);
        if (valid()) await this.router.navigate(['/artifacts', artifact.resource.id]);
      } else {
        const next = await this.api.chat(source.id, detail.record.id, detail.record.revision);
        if (valid()) {
          this.transfer.put(next.conversation.id, next.prompt);
          await this.router.navigate(['/chat', next.conversation.id]);
        }
      }
    } catch (error2) {
      if (valid()) this.actionError.set(this.scope.message(error2));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  label(kind: string) {
    const names: Record<string, string> = {
      document: '公文',
      reference: '規範／表單',
      organization: '單位資料',
      approved: '核准',
      returned: '退回',
      version: '版本更新',
      created: '建立',
    };
    return names[kind] ?? kind;
  }
  readonly date = formatDate;
}
