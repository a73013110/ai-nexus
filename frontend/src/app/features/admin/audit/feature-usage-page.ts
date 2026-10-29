import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { catalogFeature, catalogModule, routeOf } from '@core/api/catalog';
import type { FeatureUsageDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate, formatDay, formatNumber, formatPercent } from '@shared/browser/format';
import { DataTable, DataTableRow, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { RefreshButton } from '@shared/ui/refresh-button';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { TrendChart } from '@shared/ui/trend-chart';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { AuditApi } from './audit-api';

const PERIODS: ViewOption[] = [
  { value: '7', label: '7 天' },
  { value: '30', label: '30 天' },
  { value: '90', label: '90 天' },
];
const DAY = 86_400_000;

/** 期間的起訖：迄日是明天零時（含今天整天），起日往前推足天數；以瀏覽器時區分日。 */
function period(days: number, now = new Date()) {
  const until = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
  return { from: new Date(until.getTime() - days * DAY), until };
}
/** 稽核頁的 `from`／`until` 是含迄日的 `YYYY-MM-DD`。 */
const isoDay = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

/**
 * 功能使用量：依稽核紀錄統計各頁面功能的使用人數、次數與失敗。只有宣告稽核的操作才算，
 * 所以從未呼叫稽核端點的頁面不會出現；點一列看每日趨勢與使用最多的人，並可帶條件到活動稽核。
 */
@Component({
  selector: 'nx-feature-usage-page',
  imports: [
    DataTable,
    DataTableRow,
    DetailDrawer,
    FeaturePage,
    Icon,
    Notice,
    PendingButton,
    RefreshButton,
    RouterLink,
    StatGrid,
    StatTile,
    TableState,
    TrendChart,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrls: ['../../../shared/ui/figure-table.scss', './feature-usage-page.scss'],
  templateUrl: './feature-usage-page.html',
})
export class FeatureUsagePage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AuditApi);
  readonly routeOf = routeOf;
  readonly periods = PERIODS;
  readonly format = formatNumber;
  readonly days = signal('30');
  private readonly range = computed(() => period(Number(this.days())));
  readonly read = apiResource({
    feature: 'audit.usage',
    params: this.range,
    loader: ({ from, until }) => this.api.usage(from.toISOString(), until.toISOString()),
  });
  readonly report = computed(() => this.read.value() ?? null);
  readonly rows = computed(() =>
    (this.report()?.features ?? []).map((usage) => this.present(usage)),
  );
  private readonly maxUsers = computed(() => Math.max(1, ...this.rows().map((row) => row.users)));
  readonly tiles = computed(() => {
    const rows = this.rows(),
      ready = !!this.report();
    const sum = (pick: (row: FeatureUsageDto) => number) =>
      formatNumber(rows.reduce((total, row) => total + pick(row), 0));
    return [
      {
        key: 'features',
        label: '有人使用的功能',
        value: ready ? formatNumber(rows.length) : null,
        unit: '個',
      },
      {
        key: 'events',
        label: '操作次數',
        value: ready ? sum((row) => row.events) : null,
        unit: '次',
      },
      {
        key: 'failures',
        label: '失敗或拒絕',
        value: ready ? sum((row) => row.failures) : null,
        unit: '次',
      },
    ];
  });
  readonly selectedId = signal<string | null>(null);
  readonly selected = computed(
    () => this.rows().find((row) => row.featureId === this.selectedId()) ?? null,
  );
  readonly drawer = viewChild(DetailDrawer);
  private readonly detailParams = computed(() => {
    const featureId = this.selectedId();
    return featureId ? { featureId, ...this.range() } : undefined;
  });
  readonly detailRead = apiResource({
    feature: 'audit.usage',
    params: this.detailParams,
    loader: ({ featureId, from, until }) =>
      this.api.usageDetail(
        featureId,
        from.toISOString(),
        until.toISOString(),
        -new Date().getTimezoneOffset(),
      ),
  });
  readonly detail = computed(() => {
    const detail = this.detailRead.value();
    return detail && detail.featureId === this.selectedId() ? detail : null;
  });
  readonly trend = computed(() =>
    (this.detail()?.days ?? []).map((day) => ({ label: day.date.slice(5), value: day.users })),
  );
  /** 帶到活動稽核：同一功能、同一期間（稽核頁的迄日含當天，所以減一天）。 */
  readonly auditQuery = computed(() => {
    const { from, until } = this.range();
    return {
      feature: this.selectedId(),
      from: isoDay(from),
      until: isoDay(new Date(until.getTime() - DAY)),
    };
  });
  readonly periodLabel = computed(() => {
    const report = this.report();
    return report
      ? `${formatDay(report.from)} – ${formatDay(new Date(Date.parse(report.until) - 1).toISOString())}`
      : '';
  });

  private present(usage: FeatureUsageDto) {
    const feature = catalogFeature(usage.featureId);
    return {
      ...usage,
      name: feature?.name ?? usage.featureId,
      icon: feature?.icon ?? 'chart',
      module: feature ? (catalogModule(feature.module)?.name ?? '') : '',
      lastUsed: usage.lastUsedAt ? formatDate(usage.lastUsedAt) : '—',
    };
  }
  share(users: number) {
    return users / this.maxUsers();
  }
  failureRate(row: FeatureUsageDto) {
    const total = row.events + row.failures;
    return total ? formatPercent(row.failures / total) : '—';
  }
  open(featureId: string) {
    this.selectedId.set(featureId);
    this.drawer()?.open();
  }
}
