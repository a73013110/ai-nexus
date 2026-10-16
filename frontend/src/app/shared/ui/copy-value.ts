import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CopyFeedback } from '../browser/copy-feedback';
import { Icon } from './icon';

/**
 * 值加上複製按鈕：詳情中的識別碼（TraceId、RunId）以等寬字顯示；值已顯示在別處時（`IssueCode`）用
 * `showValue=false` 只留按鈕。複製結果由 status 區域報讀一次，不改按鈕名稱，避免螢幕報讀器重複唸。
 */
@Component({
  selector: 'nx-copy-value',
  imports: [Icon],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-copy-value' },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--p-space-1);
      min-width: 0;
      max-width: 100%;
      vertical-align: middle;
    }
    code {
      min-width: 0;
      overflow-wrap: anywhere;
      font-family: var(--font-code);
      font-size: var(--p-font-xs);
    }
    :host(.is-truncated) code {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .icon-button {
      flex: none;
      width: 1.5rem;
      height: 1.5rem;
    }
    .icon-button nx-icon {
      --icon-size: var(--icon-sm);
    }
    @media (pointer: coarse) {
      .icon-button {
        width: var(--button-target);
        height: var(--button-target);
      }
    }
  `,
  template: `@if (value()) {
    @if (showValue()) {
      <code [title]="value()">{{ value() }}</code>
    }
    <button
      type="button"
      [class]="compact() ? 'icon-button' : 'secondary-button'"
      [attr.aria-label]="'複製' + label()"
      [title]="'複製' + label()"
      (click)="copy()"
    >
      @if (compact()) {
        <nx-icon [name]="copied() ? 'check' : 'copy'" />
      } @else {
        {{ copied() ? '已複製' : '複製' + label() }}
      }</button
    ><span [class]="compact() ? 'sr-only' : 'form-note'" role="status">{{ notice() }}</span>
  }`,
})
export class CopyValue {
  readonly value = input<string | null | undefined>();
  /** 值的名稱，用於按鈕與報讀，例如「TraceId」。 */
  readonly label = input.required<string>();
  readonly showValue = input(true);
  readonly compact = input(true);
  private readonly feedback = inject(CopyFeedback);
  readonly copied = this.feedback.copied;
  readonly notice = computed(
    () => this.feedback.error() || (this.copied() ? this.label() + '已複製。' : ''),
  );
  async copy() {
    const value = this.value();
    if (value) await this.feedback.copy(value);
  }
}
