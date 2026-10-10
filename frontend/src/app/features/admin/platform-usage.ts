import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '../../core/api/api-resource';
import {
  formatBytes,
  formatDate,
  formatDuration,
  formatModelDisplayName,
  formatNumber,
} from '../../shared/browser/format';
import { Card } from '../../shared/ui/card';
import { DataTable } from '../../shared/ui/data-table';
import { Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { AdminApi } from './admin-api';

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

/** The last 30 days of platform usage, read each time the tab is shown. */
@Component({
  selector: 'nx-platform-usage',
  imports: [RouterLink, Card, DataTable, Icon, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './platform-usage.scss',
  templateUrl: './platform-usage.html',
})
export class PlatformUsage {
  private readonly api = inject(AdminApi);
  readonly prices = output<void>();
  readonly read = apiResource({ feature: 'admin', loader: () => this.api.usage() });
  readonly usageModelName = formatModelDisplayName;
  readonly date = formatDate;
  readonly bytes = formatBytes;
  readonly duration = formatDuration;
  readonly format = formatNumber;
  usageKindName(kind: string) {
    return kindNames[kind] || kind;
  }
}
