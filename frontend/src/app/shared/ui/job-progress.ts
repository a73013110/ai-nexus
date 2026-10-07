import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Job } from '../../core/api/types';
import { newLocalIssue, systemProblem, validIssueCode } from '../../core/api/safe-errors';
import { IssueCode } from './issue-code';
import { InferenceSignal } from './inference-signal';

@Component({
  selector: 'nx-job-progress',
  imports: [InferenceSignal, IssueCode],
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
      @if (job().totalUnits; as total) {
        <span>{{ job().completedUnits }} / {{ total }}</span>
      }
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
      <p class="job-error">{{ failure() }}<nx-issue-code [message]="failure()" /></p>
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
  readonly job = input.required<Job>();
  readonly active = computed(() => ['queued', 'running'].includes(this.job().status));
}
