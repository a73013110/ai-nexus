import { IssueCode } from './issue-code';
import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  viewChild,
} from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { CopyFeedback } from '../browser/copy-feedback';
import { renderMarkdownWithDiagrams } from './markdown';
import { completeStreamingInline } from './streaming-markdown';

@Component({
  imports: [IssueCode],
  selector: 'nx-markdown-view',
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #body class="markdown" [innerHTML]="html()" (click)="copyCode($event)"></div>
    @if (copy.error()) {
      <p class="message-note" role="status">
        {{ copy.error() }}<nx-issue-code [message]="copy.error()" />
      </p>
    }`,
})
export class MarkdownView {
  readonly content = input.required<string>();
  readonly streaming = input(false);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly injector = inject(Injector);
  private readonly body = viewChild.required<ElementRef<HTMLElement>>('body');
  private clearWidgets: () => void = () => undefined;
  private revision = 0;
  readonly copy = inject(CopyFeedback);
  private readonly rendered = computed(() =>
    renderMarkdownWithDiagrams(
      this.streaming() ? completeStreamingInline(this.content()) : this.content(),
      this.streaming(),
    ),
  );
  readonly html = computed(() => this.sanitizer.bypassSecurityTrustHtml(this.rendered().html));
  constructor() {
    afterRenderEffect(() => {
      const { diagrams } = this.rendered();
      const host = this.body().nativeElement;
      const revision = ++this.revision;
      this.clearWidgets();
      if (diagrams.length) void this.mountDiagrams(host, diagrams, revision);
    });
    inject(DestroyRef).onDestroy(() => {
      ++this.revision;
      this.clearWidgets();
    });
  }
  private async mountDiagrams(host: HTMLElement, diagrams: readonly string[], revision: number) {
    const slots = [...host.querySelectorAll<HTMLElement>('.markdown-diagram-slot')];
    try {
      const { mountMarkdownDiagrams } = await import('./markdown-widgets');
      if (revision !== this.revision) return;
      this.clearWidgets = mountMarkdownDiagrams(host, diagrams, this.injector);
    } catch {
      if (revision !== this.revision) return;
      for (const slot of slots) {
        const note = document.createElement('p');
        note.textContent = '圖表元件未能載入，以下保留 Mermaid 原始碼。';
        note.setAttribute('role', 'status');
        const pre = document.createElement('pre');
        const code = document.createElement('code');
        code.textContent = diagrams[Number(slot.dataset['diagramIndex'])] || '';
        pre.append(code);
        slot.replaceChildren(note, pre);
      }
    }
  }
  async copyCode(event: MouseEvent) {
    const button = event.target instanceof Element ? event.target.closest('.code-copy') : null,
      code = button?.closest('.code-block')?.querySelector('code');
    if (button && code) await this.copy.copy(code.textContent || '', button);
  }
}
