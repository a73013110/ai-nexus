import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { generationStatus } from '../../core/api/generation-status';
import { CountBadge, type BadgeTone } from '../../shared/ui/count-badge';
import { EmptyState } from '../../shared/ui/empty-state';
import { GenerationIndicator } from '../../shared/ui/generation-indicator';
import { Icon } from '../../shared/ui/icon';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { InfoPopover } from '../../shared/ui/info-popover';
import { NexusIsland } from '../../shared/ui/island';
import { IslandService, type IslandItem } from '../../shared/ui/island-service';
import { Notice } from '../../shared/ui/notice';
import { Select } from '../../shared/ui/select';
import { StatusBadge } from '../../shared/ui/status-badge';
import { JobProgress } from '../tasks/job-progress';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples, jobStates } from './design-samples';

type IslandSample = 'live' | 'warning' | 'info';

/** Notices, the island, badges, progress and empty states. */
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
    NexusIsland,
    Notice,
    Select,
    StatusBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './design-feedback.scss',
  templateUrl: './design-feedback.html',
})
export class DesignFeedback {
  readonly samples = inject(DesignSamples);
  readonly states = jobStates;
  readonly generation = computed(() => generationStatus(this.samples.demo().status, false));
  /** The island shows whatever is registered while this page is open, so only local samples. */
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
