import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { generationStatus } from '@core/api/generation-status';
import { CountBadge, type BadgeTone } from '@shared/ui/count-badge';
import { EmptyState } from '@shared/ui/empty-state';
import { GenerationIndicator } from '@shared/ui/generation-indicator';
import { Icon } from '@shared/ui/icon';
import { InferenceSignal } from '@shared/ui/inference-signal';
import { InfoPopover } from '@shared/ui/info-popover';
import { Island } from '@shared/ui/island';
import { IslandService, type IslandItem } from '@shared/ui/island-service';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { ToastHistory } from '@shared/ui/toast-history';
import { ToastService } from '@shared/ui/toast-service';
import { ViewScope } from '@shared/browser/view-scope';
import { LiveDot } from '@shared/ui/live-dot';
import { JobProgress } from '@features/jobs/job-progress';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples, jobStates } from './design-samples';

type IslandSample = 'live' | 'warning' | 'info';

/** 通知、島、徽章、進度與空狀態。 */
@Component({
  selector: 'nx-design-feedback',
  imports: [
    CountBadge,
    DesignExample,
    DesignSection,
    EmptyState,
    GenerationIndicator,
    Icon,
    InferenceSignal,
    InfoPopover,
    JobProgress,
    LiveDot,
    Island,
    Notice,
    Select,
    StatusBadge,
    ToastHistory,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './design-feedback.scss',
  templateUrl: './design-feedback.html',
})
export class DesignFeedback {
  readonly samples = inject(DesignSamples);
  readonly toast = inject(ToastService);
  private readonly scope = inject(ViewScope);
  private burst = 0;
  readonly states = jobStates;
  readonly generation = computed(() => generationStatus(this.samples.demo().status, false));
  /** 頁面開啟期間島會顯示所有已登記的內容，因此這裡只用本地範例。 */
  readonly islandItems = signal<IslandItem[]>([]);
  readonly islandSamples: Record<IslandSample, IslandItem> = {
    live: { id: 'live', priority: 50, tone: 'live', icon: 'activity', label: '正在生成回答' },
    warning: {
      id: 'warning',
      priority: 80,
      tone: 'warning',
      icon: 'info',
      label: '超出 Context 預算',
      message: '本次提問與附件超出 Context 預算，請縮短內容或減少附件。',
      urgent: true,
    },
    info: {
      id: 'info',
      priority: 10,
      tone: 'info',
      icon: 'globe',
      label: '網路搜尋',
      message: '會將這次提問送往搜尋服務；附件與歷史對話不會送出。',
      action: { label: '關閉', run: () => this.toggleIsland('info') },
    },
  };
  readonly islandButtons: { kind: IslandSample; label: string }[] = [
    { kind: 'live', label: '生成狀態' },
    { kind: 'info', label: '一般提示' },
    { kind: 'warning', label: '緊急警告' },
  ];
  readonly badgeTones: { tone: BadgeTone; label: string; count: number }[] = [
    { tone: 'neutral', label: '一般', count: 2 },
    { tone: 'info', label: '資訊', count: 8 },
    { tone: 'success', label: '成功', count: 4 },
    { tone: 'warning', label: '注意', count: 12 },
    { tone: 'danger', label: '錯誤', count: 3 },
  ];
  constructor() {
    inject(IslandService).register(this.islandItems);
  }
  toastSuccess() {
    this.toast.success('變更已儲存', '會套用於此帳號的其他裝置。');
  }
  toastUndo() {
    this.toast.show({
      tone: 'info',
      title: '已封存 3 則示範對話',
      action: { label: '復原', run: () => this.toast.success('已復原封存') },
    });
  }
  /** 同一個 id 讓提示從進行中到完成，因此會原地更新。 */
  toastUpload() {
    const id = 'design-upload';
    this.toast.show({ id, tone: 'info', title: '正在上傳 季度報告.pdf', duration: 0 });
    this.scope.later(
      () => this.toast.show({ id, title: '文件已上傳', description: '文字辨識會在背景繼續處理。' }),
      1600,
      id,
    );
  }
  toastWarning() {
    this.toast.show({
      tone: 'warning',
      title: '部分檔案未加入',
      description: '2 份檔案超過 20 MB 上限，其餘已加入。',
    });
  }
  toastBurst() {
    for (const [index, title] of ['來源已移除', '已安排重新索引', '知識庫已儲存'].entries())
      this.scope.later(
        () => this.toast.success(title),
        index * 180,
        `design-burst-${++this.burst}`,
      );
  }
  hasIsland(kind: IslandSample) {
    return this.islandItems().some((item) => item.id === kind);
  }
  toggleIsland(kind: IslandSample) {
    this.islandItems.update((items) =>
      items.some((item) => item.id === kind)
        ? items.filter((item) => item.id !== kind)
        : [...items, this.islandSamples[kind]],
    );
  }
}
