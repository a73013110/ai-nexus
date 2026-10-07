import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { issueInMessage } from '../../core/api/safe-errors';

@Component({
  selector: 'nx-issue-code',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: inline; } button { margin-inline-start: .5rem; min-height: 44px; }',
  template: `@if (code()) {
    <button type="button" class="secondary-button" aria-label="複製問題查證代碼" (click)="copy()">
      {{ copied() ? '已複製' : '複製代碼' }}</button
    ><span class="form-note" role="status">{{ notice() }}</span>
  }`,
})
export class IssueCode {
  readonly message = input<string | null | undefined>();
  readonly code = computed(() => issueInMessage(this.message()));
  readonly copied = signal(false);
  readonly notice = signal('');
  async copy() {
    const code = this.code();
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.copied.set(true);
      this.notice.set('查證代碼已複製。');
    } catch {
      this.notice.set('無法使用剪貼簿，請選取文字複製代碼。');
    }
  }
}
