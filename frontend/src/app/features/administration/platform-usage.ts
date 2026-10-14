import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import {
  formatBytes,
  formatDay,
  formatDuration,
  formatModelDisplayName,
  formatNumber,
  formatPercent,
} from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';
import { DataTable } from '@shared/ui/data-table';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { StatusBadge } from '@shared/ui/status-badge';
import { providerName } from '@features/billing/billing-api';
import { AdministrationApi } from './administration-api';

const kindNames: Record<string, string> = {
  chat: '對話',
  transform: '文字處理',
  evaluation: '評測',
  ocr: '圖片辨識',
  embedding: '知識向量',
  rerank: '知識重排',
  'query-rewrite': '檢索查詢改寫',
  'web-search': '網路搜尋',
};

interface ServiceRow {
  id: string;
  name: string;
  tone: BadgeTone;
  status: string;
  notice: string;
}

/**
 * The last 30 days of platform usage, read each time the tab is shown: four headline figures, then
 * the models, the sources of use and the state of the model and search services.
 */
@Component({
  selector: 'nx-platform-usage',
  imports: [DataTable, EmptyState, Icon, Notice, StatGrid, StatTile, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./operations-tabs.scss', './platform-usage.scss'],
  templateUrl: './platform-usage.html',
})
export class PlatformUsage {
  private readonly api = inject(AdministrationApi);
  readonly read = apiResource({ feature: 'operations', loader: () => this.api.usage() });
  readonly stats = computed(() => this.read.value() ?? null);
  readonly format = formatNumber;
  readonly modelName = formatModelDisplayName;
  readonly duration = formatDuration;
  /** The period's days; the API reads a fixed 30 days ending now. */
  readonly period = computed(() => {
    const stats = this.stats();
    return stats ? `${formatDay(stats.since)} – ${formatDay(stats.until)}` : '';
  });
  readonly tiles = computed(() => {
    const stats = this.stats(),
      n = formatNumber;
    return [
      {
        key: 'users',
        label: '啟用帳號',
        value: stats ? n(stats.users) : null,
        unit: '位',
        hint: stats ? `${n(stats.activeUsers)} 位在區間內使用 AI` : '',
      },
      {
        key: 'requests',
        label: '生成次數',
        value: stats ? n(stats.requests) : null,
        unit: '次',
        hint: stats
          ? `完成 ${n(stats.completed)} · 失敗 ${n(stats.failed)} · 取消 ${n(stats.cancelled)}`
          : '',
      },
      {
        key: 'tokens',
        label: '回報 tokens',
        value: stats ? n(stats.inputTokens + stats.outputTokens) : null,
        unit: '',
        hint: stats ? `輸入 ${n(stats.inputTokens)} · 輸出 ${n(stats.outputTokens)}` : '',
      },
      {
        key: 'storage',
        label: '原檔容量',
        value: stats ? formatBytes(stats.storedBytes) : null,
        unit: '',
        hint: stats ? `${n(stats.storedFiles)} 份原檔 · 所有期間，重複引用只計一次` : '',
      },
    ];
  });
  readonly models = computed(() =>
    [...(this.stats()?.models ?? [])].sort((a, b) => b.requests - a.requests),
  );
  readonly kinds = computed(() =>
    [...(this.stats()?.kinds ?? [])].sort((a, b) => b.requests - a.requests),
  );
  readonly kindTotal = computed(() => this.kinds().reduce((sum, x) => sum + x.requests, 0));
  readonly services = computed<ServiceRow[]>(() => {
    const stats = this.stats();
    if (!stats) return [];
    return [
      ...stats.providers.map((provider) => ({
        id: provider.id,
        name: providerName(provider.id),
        tone: (provider.available ? 'success' : 'danger') as BadgeTone,
        status: provider.available ? '已連線' : '無法連線',
        notice: provider.notice || '模型目錄可正常讀取；圖片能力依各模型實際回報判定。',
      })),
      {
        id: 'web-search',
        name: '網路搜尋',
        tone: stats.webSearch.available ? 'success' : 'neutral',
        status: stats.webSearch.available ? '已設定' : '尚未啟用',
        notice: stats.webSearch.notice,
      },
    ];
  });
  kindName(kind: string) {
    return kindNames[kind] || kind;
  }
  share(part: number, total: number) {
    return total ? formatPercent(part / total) : '—';
  }
}
