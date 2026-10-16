import { ChangeDetectionStrategy, Component, computed, input, viewChild } from '@angular/core';
import { issueInMessage } from '@core/errors/safe-errors';
import { CopyValue } from './copy-value';

/** The copy button for the issue code inside an error message; the message shows the code. */
@Component({
  selector: 'nx-issue-code',
  imports: [CopyValue],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: inline; } nx-copy-value { margin-inline-start: .25rem; }',
  template: `@if (code()) {
    <nx-copy-value
      [value]="code()"
      label="問題查證代碼"
      [showValue]="false"
      [compact]="compact()"
    />
  }`,
})
export class IssueCode {
  readonly message = input<string | null | undefined>();
  readonly compact = input(true);
  private readonly button = viewChild(CopyValue);
  readonly code = computed(() => issueInMessage(this.message()));
  readonly copied = computed(() => this.button()?.copied() ?? false);
  async copy() {
    await this.button()?.copy();
  }
}
