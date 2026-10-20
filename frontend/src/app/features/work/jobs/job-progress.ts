import { Notice } from '@shared/ui/notice';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { JobDto } from '@core/api/schema';
import { isActive } from '@core/api/generation-status';
import { InferenceSignal } from '@shared/ui/inference-signal';
import { StatusBadge } from '@shared/ui/status-badge';
import { jobFailure, jobStatus } from './job-kind';

/**
 * 背景工作的階段、進度與失敗訊息。子內容是動作按鈕（停止、重試），放在狀態徽章旁；用預設插槽，
 * 呼叫端以巢狀 `@if` 決定顯示哪個按鈕也能投影（具名 `select` 遇到巢狀控制流程會整段丟掉）。
 */
@Component({
  selector: 'nx-job-progress',
  imports: [Notice, InferenceSignal, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './job-progress.scss',
  template: `<div class="job-progress" [attr.data-status]="job().status">
    <div class="job-stage">
      <span class="job-stage-label">
        @if (active()) {
          <nx-inference-signal [active]="true" />
        }
        <span>{{ job().stage }}</span></span
      >
      <div class="job-stage-meta">
        <nx-status-badge [tone]="status().tone">{{ status().label }}</nx-status-badge>
        @if (job().totalUnits; as total) {
          <span>{{ job().completedUnits }} / {{ total }}</span>
        }
        <ng-content />
      </div>
    </div>
    @if (active()) {
      @if (job().totalUnits; as total) {
        <progress
          [value]="job().completedUnits"
          [max]="total"
          [attr.aria-label]="job().stage"
        ></progress>
      } @else {
        <progress [attr.aria-label]="job().stage"></progress>
      }
    }
    @if (job().cancelRequested && active()) {
      <p class="form-note" role="status">已提出停止要求，正在結束目前的步驟。</p>
    }
    @if (job().errorMessage) {
      <nx-notice tone="danger" [message]="failure()" />
    }
  </div>`,
})
export class JobProgress {
  readonly failure = computed(() => jobFailure(this.job()));
  readonly job = input.required<JobDto>();
  readonly active = computed(() => isActive(this.job().status));
  readonly status = computed(() => jobStatus(this.job().status));
}
