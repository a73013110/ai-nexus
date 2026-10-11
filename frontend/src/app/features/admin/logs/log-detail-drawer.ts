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
import { apiResource } from '../../../core/api/api-resource';
import type { DiagnosticSummary } from '../../../core/api/schema';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { CodeBlock } from '../../../shared/markdown/code-block';
import { formatDate } from '../../../shared/browser/format';
import { DetailDrawer } from '../../../shared/ui/detail-drawer';
import { Icon } from '../../../shared/ui/icon';
import { IssueCode } from '../../../shared/ui/issue-code';
import { Notice } from '../../../shared/ui/notice';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { Tabs, type TabItem } from '../../../shared/ui/tabs';
import { ViewMotion } from '../../../shared/ui/view-motion';
import { emptyLogFields } from './log-filter-state';
import { levelTone, milliseconds } from './log-display';
import { SystemLogsApi, type LogFilter } from './system-logs-api';

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

/** One log event's detail, masked properties, exception and correlated flow. */
@Component({
  selector: 'nx-log-detail-drawer',
  imports: [
    CodeBlock,
    DetailDrawer,
    Icon,
    IssueCode,
    Notice,
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
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(SystemLogsApi);
  private readonly drawer = viewChild.required(DetailDrawer);
  readonly current = input<DiagnosticSummary | null>(null);
  /** Position of `current` in the shown page, or -1. */
  readonly index = input(-1);
  readonly count = input(0);
  /** The submitted query, whose time range bounds the correlated flow. */
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
  /** Opening the drawer starts on the overview; moving between events keeps the tab. */
  readonly detailTab = linkedSignal({
    source: () => !!this.current(),
    computation: () => 'overview',
  });
  private readonly detailRead = apiResource({
    feature: 'logs.detail',
    params: () => this.current()?.logId || undefined,
    loader: async (id, signal) => ({ id, data: await this.api.detail(id, signal) }),
  });
  readonly detail = computed(() => {
    const loaded = this.detailRead.value();
    return loaded && loaded.id === this.current()?.logId ? loaded.data : null;
  });
  readonly detailLoading = this.detailRead.refreshing;
  readonly detailError = this.detailRead.error;
  private readonly flow = computed(() => {
    const entry = this.current(),
      range = this.range();
    return entry && range && (entry.traceId || entry.jobId || entry.runId)
      ? { entry, from: range.from, to: range.to }
      : undefined;
  });
  private readonly relatedRead = apiResource({
    feature: 'logs.query',
    params: this.flow,
    loader: async ({ entry, from, to }, signal) => {
      const filter: LogFilter = {
        ...emptyLogFields(),
        from,
        to,
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
  readonly date = formatDate;
  readonly tone = levelTone;
  readonly milliseconds = milliseconds;
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
