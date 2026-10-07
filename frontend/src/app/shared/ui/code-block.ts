import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { CopyFeedback } from '../browser/copy-feedback';
import { Icon } from './icon';

@Component({
  selector: 'nx-code-block',
  imports: [Icon],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
      border: 1px solid var(--line);
      border-radius: var(--p-radius-md);
      overflow: clip;
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      background: var(--surface-subtle);
      border-bottom: 1px solid var(--line);
      font-size: var(--p-font-xs);
      color: var(--secondary);
    }
    pre {
      margin: 0;
      padding: 0.75rem;
      background: var(--code);
      font: var(--p-font-data)/1.65 var(--font-code);
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      tab-size: 2;
    }
    .error {
      padding: 0.5rem 0.75rem;
      margin: 0;
      color: var(--error);
      font-size: var(--p-font-sm);
    }
    .icon-button nx-icon {
      width: 16px;
      height: 16px;
    }
  `,
  template: `<header>
      <span>{{ label() }}</span>
      <button
        type="button"
        class="icon-button"
        [attr.aria-label]="'複製' + label()"
        [title]="'複製' + label()"
        (click)="feedback.copy(value())"
      >
        <nx-icon [name]="feedback.copied() ? 'check' : 'copy'" /></button
      ><span class="sr-only" role="status">{{ feedback.copied() ? label() + '已複製' : '' }}</span>
    </header>
    <pre><code>{{ value() }}</code></pre>
    @if (feedback.error()) {
      <p class="error" role="alert">{{ feedback.error() }}</p>
    }`,
})
export class CodeBlock {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly feedback = inject(CopyFeedback);
}
