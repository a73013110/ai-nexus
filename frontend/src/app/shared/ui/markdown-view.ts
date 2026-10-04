import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { CopyFeedback } from '../browser/copy-feedback';
import { renderMarkdown } from './markdown';

@Component({
  selector: 'nx-markdown-view',
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="markdown" [innerHTML]="html()" (click)="copyCode($event)"></div>
    @if (copy.error()) {
      <p class="message-note" role="status">{{ copy.error() }}</p>
    }`,
})
export class MarkdownView {
  readonly content = input.required<string>();
  private readonly sanitizer = inject(DomSanitizer);
  readonly copy = inject(CopyFeedback);
  readonly html = computed(() =>
    this.sanitizer.bypassSecurityTrustHtml(renderMarkdown(this.content())),
  );
  async copyCode(event: MouseEvent) {
    const button = event.target instanceof Element ? event.target.closest('.code-copy') : null,
      code = button?.closest('.code-block')?.querySelector('code');
    if (button && code) await this.copy.copy(code.textContent || '', button);
  }
}
