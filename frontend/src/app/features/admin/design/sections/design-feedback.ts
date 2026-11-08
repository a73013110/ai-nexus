import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { generationStatus } from '@core/api/generation-status';
import { AiMark, type AiMarkState } from '@shared/ui/ai-mark';
import { CountBadge, type BadgeTone } from '@shared/ui/count-badge';
import { EmptyState } from '@shared/ui/empty-state';
import { FloatingPanel } from '@shared/ui/floating-panel';
import { GenerationIndicator } from '@shared/ui/generation-indicator';
import { Icon } from '@shared/ui/icon';
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
import { JobProgress } from '@features/work/jobs/job-progress';
import { AssistantConversation } from '@features/platform/assistant/assistant-conversation';
import { AssistantPanel } from '@features/platform/assistant/assistant-panel';
import type { ModelsDto } from '@core/api/schema';
import { ApiError } from '@core/errors/safe-errors';
import { DesignRunTiming } from './design-run-timing';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples, jobStates } from './design-samples';

type IslandSample = 'live' | 'warning' | 'info';

const DESIGN_MODELS = {
  models: [
    {
      id: 'demo',
      displayName: '示範模型',
      contextTokens: 32768,
      supportsImages: false,
      reasoningEfforts: [],
      defaultReasoningEffort: 'auto',
    },
    {
      id: 'demo-large',
      displayName: '示範模型（大）',
      contextTokens: 131072,
      supportsImages: true,
      reasoningEfforts: [],
      defaultReasoningEffort: 'auto',
    },
  ],
  policy: {
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: 'demo',
    maxInputCharacters: 12000,
  },
  notice: null,
} as unknown as ModelsDto;

/** 假的 SSE 回應：依據一次送出，回答每 120 ms 一段。 */
function designAnswer(signal: AbortSignal) {
  const events: [string, object][] = [
    [
      'grounding',
      {
        scope: '2026/10/01–10/31 · 2 人',
        partial: false,
        facts: [
          { label: '查詢範圍', text: '2026/10/01–10/31，今天 10/22。' },
          {
            label: '陳雅婷（yating）',
            text: '已結算 15 天：正常 13 天、早退 2 天；未補足 2 天（共差 3 小時 10 分）。',
          },
          {
            label: '林志明（chihming）',
            text: '已結算 15 天：正常 14 天、遲到 1 天；流程中 1 天。',
          },
          {
            label: '林志明 10/14（二）',
            text: '流程中的單 L-2031（特休）停在 黃主任（核准關卡）。',
          },
        ],
      },
    ],
    ...[
      '**陳雅婷**還有 2 天未補足，',
      '共差 3 小時 10 分 [2]。\n\n',
      '**林志明**的特休單 L-2031 ',
      '還在黃主任核准 [4]。',
    ].map((text): [string, object] => ['delta', { text }]),
    ['done', { truncated: false }],
  ];
  const encoder = new TextEncoder();
  return new Response(
    new ReadableStream({
      async start(controller) {
        for (const [event, data] of events) {
          await new Promise((resolve) => setTimeout(resolve, 120));
          if (signal.aborted) return controller.error(new DOMException('已停止', 'AbortError'));
          controller.enqueue(encoder.encode(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`));
        }
        controller.close();
      },
    }),
  );
}

/** 通知、島、徽章、進度與空狀態。 */
@Component({
  selector: 'nx-design-feedback',
  imports: [
    AiMark,
    AssistantPanel,
    CountBadge,
    DesignExample,
    DesignRunTiming,
    DesignSection,
    EmptyState,
    FloatingPanel,
    GenerationIndicator,
    Icon,
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
  readonly markStates: { state: AiMarkState; label: string }[] = [
    { state: 'rest', label: '靜止' },
    { state: 'idle', label: '待命' },
    { state: 'working', label: '工作中' },
    { state: 'waiting', label: '排隊中' },
  ];
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
  private failing = false;
  /** 助理的本機範例：不呼叫 AI，依據與回答是寫死的假資料，逐段送出模擬串流。 */
  readonly assistant = new AssistantConversation({
    context: () => 'design',
    models: async () => DESIGN_MODELS,
    preferredModelId: () => null,
    ask: async (_, signal) => {
      if (this.failing) {
        this.failing = false;
        throw new ApiError(400, 'assistant_question_invalid');
      }
      return designAnswer(signal);
    },
  });
  constructor() {
    inject(IslandService).register(this.islandItems);
  }
  failNext() {
    this.failing = true;
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
