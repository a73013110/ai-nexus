import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { DiagnosticSummary } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { CodeBlock } from '@shared/markdown/code-block';
import { formatDate } from '@shared/browser/format';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { Icon } from '@shared/ui/icon';
import { CopyValue } from '@shared/ui/copy-value';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { EmptyState } from '@shared/ui/empty-state';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatusBadge } from '@shared/ui/status-badge';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { levelTone, milliseconds } from './log-display';
import { DiagnosticsApi, emptyLogFilter, type LogFilter } from './diagnostics-api';
import { logDetailTour } from './diagnostics-tour';
import { routeOf } from '@core/api/catalog';

const requestFields = [
  ['ClientAddress', '連線 IP'],
  ['UserAgent', 'User-Agent（用戶端提供）'],
  ['RequestProtocol', 'HTTP 協定'],
  ['RequestScheme', '傳輸方式'],
  ['RequestOutcome', '請求結果'],
  ['RequestAborted', '用戶端中止'],
  ['ResponseStarted', '回應已開始'],
];
const outcomes: Record<string, string> = {
  completed: '回應已完成',
  failed: '執行失敗',
  cancelled: '用戶端取消',
};

/** 單筆日誌的詳情：遮罩後的屬性、例外與同一流程的關聯事件。 */
@Component({
  selector: 'nx-log-detail-drawer',
  imports: [
    CodeBlock,
    CopyValue,
    DetailDrawer,
    DrawerPager,
    EmptyState,
    Icon,
    Notice,
    PendingButton,
    RouterLink,
    StatusBadge,
    Tabs,
    ViewMotion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./log-detail-drawer.scss', './log-detail-timeline.scss'],
  templateUrl: './log-detail-drawer.html',
})
export class LogDetailDrawer {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(DiagnosticsApi);
  private readonly drawer = viewChild.required(DetailDrawer);
  readonly current = input<DiagnosticSummary | null>(null);
  /** `current` 在目前這一頁的位置；不在時為 -1。 */
  readonly index = input(-1);
  readonly count = input(0);
  /** 已送出的查詢；關聯流程只在它的時間範圍內找。 */
  readonly range = input<Pick<LogFilter, 'from' | 'to'> | null>(null);
  readonly move = output<-1 | 1>();
  readonly closed = output<void>();
  readonly id = computed(() => this.drawer().id);
  readonly tabs: TabItem[] = [
    { value: 'overview', label: '概覽' },
    { value: 'properties', label: '受控屬性' },
    { value: 'exception', label: '例外堆疊' },
    { value: 'timeline', label: '關聯流程' },
  ];
  /** 開啟時回到總覽分頁；上一筆、下一筆之間保留目前分頁。 */
  readonly detailTab = linkedSignal({
    source: () => !!this.current(),
    computation: () => 'overview',
  });
  private readonly detailRead = apiResource({
    feature: 'diagnostics.logs.detail',
    params: () => this.current()?.logId || undefined,
    loader: async (id, signal) => ({ id, data: await this.api.detail(id, signal) }),
  });
  readonly detail = computed(() => {
    const loaded = this.detailRead.value();
    return loaded && loaded.id === this.current()?.logId ? loaded.data : null;
  });
  readonly detailLoading = this.detailRead.refreshing;
  readonly detailError = this.detailRead.error;
  /** 骨架列的寬度（%），長短交錯才不像一整塊色塊。 */
  protected readonly skeleton = [48, 92, 76, 84, 60];
  private readonly flow = computed(() => {
    const entry = this.current(),
      range = this.range();
    return entry && range && (entry.traceId || entry.jobId || entry.runId)
      ? { entry, from: range.from, to: range.to }
      : undefined;
  });
  private readonly relatedRead = apiResource({
    feature: 'diagnostics.logs',
    params: this.flow,
    loader: async ({ entry, from, to }, signal) => {
      const filter: LogFilter = {
        ...emptyLogFilter(from, to),
        traceId: entry.traceId ?? '',
        jobId: entry.jobId ?? '',
        runId: entry.runId ?? '',
      };
      const page = await this.api.list(filter, undefined, signal);
      return { logId: entry.logId, events: [...page.events].reverse() };
    },
  });
  readonly related = computed<DiagnosticSummary[]>(() => {
    const entry = this.current();
    if (!entry) return [];
    if (!this.flow()) return [entry];
    const loaded = this.relatedRead.value();
    return loaded && loaded.logId === entry.logId ? loaded.events : [];
  });
  readonly relatedLoading = computed(() => !!this.flow() && this.relatedRead.refreshing());
  readonly relatedError = this.relatedRead.error;
  readonly properties = computed(() => {
    const raw = this.detail()?.propertiesJson ?? '{}';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  });
  /** 範本中每個 `{Name}` 與其代入值，範本才不會看起來像沒填值。 */
  readonly messageFields = computed(() => {
    const selected = this.detail();
    if (!selected) return [];
    let properties: Record<string, unknown> = {};
    try {
      properties = JSON.parse(selected.propertiesJson);
    } catch {
      // 舊資料的 metadata 格式不正確時仍列出名稱。
    }
    // 關聯值在摘要欄位（IssueCode → issueCode），不一定在屬性裡。
    const columns = selected.event as unknown as Record<string, unknown>;
    const names = new Set(
      Array.from(
        selected.event.messageTemplate
          .replace(/\{\{|\}\}/g, '')
          .matchAll(/\{([A-Za-z][\w.]*)[^{}]*\}/g),
        (match) => match[1],
      ),
    );
    return [...names].map((name) => {
      const value = properties[name] ?? columns[name.charAt(0).toLowerCase() + name.slice(1)];
      return {
        name,
        value: value === null || value === undefined || value === '' ? '—' : String(value),
      };
    });
  });
  readonly requestContext = computed(() => {
    try {
      const properties: Record<string, unknown> = JSON.parse(this.detail()?.propertiesJson ?? '{}');
      return requestFields
        .filter(
          ([key]) =>
            properties[key] !== null && properties[key] !== undefined && properties[key] !== '',
        )
        .map(([key, label]) => ({
          key,
          label,
          value:
            key === 'RequestOutcome'
              ? (outcomes[String(properties[key])] ?? String(properties[key]))
              : typeof properties[key] === 'boolean'
                ? properties[key]
                  ? '是'
                  : '否'
                : String(properties[key]),
        }));
    } catch {
      return [];
    }
  });
  /** 抽屜沒有選取事件時不給導覽。 */
  readonly tour = computed(() => {
    const entry = this.current();
    if (!entry) return null;
    return logDetailTour({
      exception: !!this.detail()?.exceptionType,
      flow: !!this.flow(),
      audit: entry.traceId && this.session.has('audit') ? this.session.featureName('audit') : null,
    });
  });
  readonly date = formatDate;
  readonly tone = levelTone;
  readonly milliseconds = milliseconds;
  /** 這筆事件有值的關聯 ID，各自可複製。 */
  identifiers(event: DiagnosticSummary) {
    return [
      { label: '查證代碼', value: event.issueCode, wide: true },
      { label: 'TraceId', value: event.traceId, wide: true },
      { label: 'SpanId', value: event.spanId, wide: false },
      { label: 'RequestId', value: event.requestId, wide: false },
      { label: 'OperationId', value: event.operationId, wide: true },
      { label: 'JobId', value: event.jobId, wide: true },
      { label: 'RunId', value: event.runId, wide: true },
      { label: 'LogId', value: event.logId, wide: true },
    ].filter((id) => !!id.value);
  }
  open() {
    this.drawer().open();
  }
  close() {
    this.drawer().close();
  }
  reloadDetail() {
    this.detailRead.reload();
    this.relatedRead.reload();
  }
  selectTab(value: string) {
    this.detailTab.set(value);
    this.drawer().resetScroll();
  }
}
