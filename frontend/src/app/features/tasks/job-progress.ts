import { Notice } from '../../shared/ui/notice';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { JobDto } from '../../core/api/schema';
import { newLocalIssue, systemProblem, validIssueCode } from '../../core/errors/safe-errors';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { StatusBadge } from '../../shared/ui/status-badge';
import type { BadgeTone } from '../../shared/ui/count-badge';

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
  private legacyId = '';
  private legacyCode = '';
  readonly failure = computed(() => {
    const job = this.job();
    if (validIssueCode(job.issueCode)) return systemProblem(job.issueCode);
    if (this.legacyId !== job.id || !this.legacyCode) {
      this.legacyId = job.id;
      this.legacyCode = newLocalIssue();
    }
    return systemProblem(this.legacyCode);
  });
  readonly job = input.required<JobDto>();
  readonly active = computed(() => ['queued', 'running'].includes(this.job().status));
  readonly status = computed(() => {
    const statuses: Record<string, { label: string; tone: BadgeTone }> = {
      queued: { label: '排隊中', tone: 'neutral' },
      running: { label: '處理中', tone: 'info' },
      completed: { label: '已完成', tone: 'success' },
      failed: { label: '處理失敗', tone: 'danger' },
      cancelled: { label: '已停止', tone: 'neutral' },
    };
    return statuses[this.job().status] ?? { label: '背景處理', tone: 'neutral' as BadgeTone };
  });
}
