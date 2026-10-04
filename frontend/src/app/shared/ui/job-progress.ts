import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Job } from '../../core/api/types';
import { InferenceSignal } from './inference-signal';

@Component({
  selector: 'nx-job-progress',
  imports: [InferenceSignal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="job-progress" [attr.data-status]="job().status">
    <div class="job-stage">
      <span>
        @if (active()) {
          <nx-inference-signal [active]="true" />
        }
        {{ job().stage }}</span
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
      <p class="job-error">{{ job().errorMessage }}</p>
    }
  </div>`,
})
export class JobProgress {
  readonly job = input.required<Job>();
  readonly active = computed(() => ['queued', 'running'].includes(this.job().status));
}
