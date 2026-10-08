import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { issueInMessage } from '../../core/api/safe-errors';
import { CopyFeedback } from '../browser/copy-feedback';
import { Icon } from './icon';

@Component({
  selector: 'nx-issue-code',
  imports: [Icon],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles:
    ':host { display: inline; } button { margin-inline-start: .25rem; } .icon-button { width: 1.5rem; height: 1.5rem; } .icon-button nx-icon { width: 14px; height: 14px; } @media (pointer: coarse) { .icon-button { width: var(--button-target); height: var(--button-target); } }',
  template: `@if (code()) {
    <button
      type="button"
      [class]="compact() ? 'icon-button' : 'secondary-button'"
      aria-label="複製問題查證代碼"
      title="複製問題查證代碼"
      (click)="copy()"
    >
      @if (compact()) {
        <nx-icon [name]="copied() ? 'check' : 'copy'" />
      } @else {
        {{ copied() ? '已複製' : '複製代碼' }}
      }</button
    ><span [class]="compact() ? 'sr-only' : 'form-note'" role="status">{{ notice() }}</span>
  }`,
})
export class IssueCode {
  readonly message = input<string | null | undefined>();
  readonly compact = input(true);
  private readonly feedback = inject(CopyFeedback);
  readonly code = computed(() => issueInMessage(this.message()));
  readonly copied = this.feedback.copied;
  readonly notice = computed(
    () => this.feedback.error() || (this.copied() ? '查證代碼已複製。' : ''),
  );
  async copy() {
    const code = this.code();
    if (!code) return;
    await this.feedback.copy(code);
  }
}
