import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { ModelTokenBudgetDto } from '@core/api/schema';
import { UserSettingsService } from '@core/preferences/user-settings';
import { TokenUsageChart } from '@features/admin/billing/token-usage-chart';
import { StorageUsage } from '@features/work/files/file-library/storage-usage';
import {
  formatBytes,
  formatDate,
  formatDuration,
  formatModelDisplayName,
  formatNumber,
} from '@shared/browser/format';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { TrendChart } from '@shared/ui/trend-chart';
import { SettingsStore } from '../settings-store';

/**
 * 由近到遠：今天各模型還能用多少 token、最近 30 天的用量、原檔容量，最後是每日走勢。
 * 讀取失敗只影響這個分頁，所以錯誤也只顯示在這裡。
 */
@Component({
  selector: 'nx-usage-tab',
  imports: [
    Notice,
    PendingButton,
    SettingGroup,
    SettingRow,
    StatGrid,
    StatTile,
    StorageUsage,
    TokenUsageChart,
    TrendChart,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './usage-tab.scss',
  template: `@if (error()) {
      <nx-notice [message]="error()"
        ><button
          nxPending
          type="button"
          notice-actions
          [pending]="refreshing()"
          (click)="store.reload()"
        >
          {{ refreshing() ? '重新載入中…' : '重新載入' }}
        </button>
      </nx-notice>
    }
    @if (policy(); as limits) {
      <nx-setting-group
        class="usage-budget"
        heading="今日 token 預算"
        [description]="'輸入與輸出合計，' + date(limits.resetsAt) + ' 重設。'"
      >
        @for (model of limits.models; track model.modelId) {
          <nx-setting-row [label]="modelName(model)" [description]="budget(model)">
            @if (model.dailyTokenLimit != null) {
              <meter
                setting-detail
                min="0"
                [max]="model.dailyTokenLimit || 1"
                [value]="model.usedTokens + model.reservedTokens"
                [attr.aria-label]="'今日 token 用量：' + modelName(model)"
              ></meter>
            }
            <span class="budget-remaining">{{
              model.remainingTokens == null ? '不限制' : '剩餘 ' + format(model.remainingTokens)
            }}</span>
          </nx-setting-row>
        } @empty {
          <p class="form-note">目前沒有可用的模型。</p>
        }
      </nx-setting-group>
    } @else if (policyRead.loading()) {
      <nx-setting-group class="usage-budget" heading="今日 token 預算">
        <div class="budget-skeleton" role="status">
          <span class="skeleton"></span><span class="skeleton"></span>
          <span class="sr-only">正在載入 token 預算…</span>
        </div>
      </nx-setting-group>
    }
    @if (usage(); as stats) {
      <nx-stat-grid label="最近 30 天用量" [busy]="usageRead.refreshing()">
        <nx-stat-tile
          label="30 天生成"
          [value]="format(stats.requests)"
          [hint]="
            stats.completed + ' 完成 · ' + stats.failed + ' 失敗 · ' + stats.cancelled + ' 取消'
          "
        />
        <nx-stat-tile
          label="回報 tokens"
          [value]="format(stats.inputTokens + stats.outputTokens)"
          [hint]="'輸入 ' + format(stats.inputTokens) + ' · 輸出 ' + format(stats.outputTokens)"
        />
        <nx-stat-tile
          label="平均耗時"
          [value]="
            stats.timedRequests
              ? duration(stats.totalDurationMilliseconds / stats.timedRequests)
              : '—'
          "
          [hint]="stats.timedRequests + ' 次有耗時紀錄'"
        />
      </nx-stat-grid>
      <nx-setting-group
        class="usage-storage"
        heading="原檔容量"
        description="草稿計入；同一原檔被多處引用只計一次。"
      >
        <nx-storage-usage [storage]="stats.storage" />
        @if (attachmentLimit() != null) {
          <p class="form-note">
            群組給你的附件空間上限是 {{ bytes(attachmentLimit()!) }}；系統上限仍適用。
          </p>
        }
      </nx-setting-group>
      <nx-setting-group heading="每日 token 走勢">
        <nx-token-usage-chart [usage]="stats.tokens" [busy]="usageRead.refreshing()" />
      </nx-setting-group>
    } @else if (usageRead.loading()) {
      <!-- 首次載入：照讀到後的版面排骨架，數字、容量與走勢不會一個個冒出來。 -->
      <nx-stat-grid label="最近 30 天用量">
        <nx-stat-tile label="30 天生成" [loading]="true" />
        <nx-stat-tile label="回報 tokens" [loading]="true" />
        <nx-stat-tile label="平均耗時" [loading]="true" />
      </nx-stat-grid>
      <nx-setting-group
        class="usage-storage"
        heading="原檔容量"
        description="草稿計入；同一原檔被多處引用只計一次。"
      >
        <nx-storage-usage [loading]="true" />
      </nx-setting-group>
      <nx-setting-group heading="每日 token 走勢">
        <nx-trend-chart [points]="[]" label="每日 Tokens" [loading]="true" />
      </nx-setting-group>
    }`,
})
export class UsageTab {
  readonly store = inject(SettingsStore);
  private readonly service = inject(UserSettingsService);
  protected readonly usageRead = apiResource({
    params: () => this.store.version(),
    loader: () => this.service.usage(),
  });
  protected readonly policyRead = apiResource({
    params: () => this.store.version(),
    loader: () => this.service.policy(),
  });
  readonly usage = computed(() => this.usageRead.value() ?? null);
  readonly policy = computed(() => this.policyRead.value() ?? null);
  readonly attachmentLimit = computed(() => this.policy()?.storedAttachmentLimitBytes ?? null);
  readonly refreshing = computed(() => this.usageRead.refreshing() || this.policyRead.refreshing());
  readonly error = computed(() =>
    this.usageRead.error() || this.policyRead.error() ? '使用狀況暫時無法取得。' : '',
  );
  readonly modelName = formatModelDisplayName;
  readonly format = formatNumber;
  readonly bytes = formatBytes;
  readonly date = formatDate;
  readonly duration = formatDuration;
  budget(model: ModelTokenBudgetDto) {
    const used =
      '已用 ' +
      formatNumber(model.usedTokens) +
      (model.reservedTokens ? '，另預留 ' + formatNumber(model.reservedTokens) : '');
    // 沒有上限時右側已寫「不限制」，說明只留用量。
    return model.dailyTokenLimit == null
      ? used
      : used + ' / 每日上限 ' + formatNumber(model.dailyTokenLimit);
  }
}
