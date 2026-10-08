import { ChangeDetectionStrategy, Component, input, model, signal } from '@angular/core';
import { MarkdownView } from './markdown-view';
import { Field } from './field';
import { ViewSwitch } from './view-switch';
import { ViewMotion } from './view-motion';

/** One document value, with a readable default and an explicit Markdown editing mode. */
@Component({
  selector: 'nx-markdown-editor',
  imports: [MarkdownView, Field, ViewSwitch, ViewMotion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './markdown-editor.scss',
  template: `<div class="markdown-editor-toolbar">
      <strong>{{ label() }}</strong>
      <nx-view-switch
        [label]="label() + '呈現方式'"
        appearance="segment"
        [options]="views"
        [value]="view()"
        (valueChange)="view.set($event)"
      />
      <ng-content select="[editor-actions]" />
    </div>
    <div class="markdown-editor-panel" [nxViewMotion]="view()">
      @if (view() === 'read') {
        <section class="markdown-editor-preview" [attr.aria-label]="label() + '預覽'" tabindex="0">
          <nx-markdown-view [content]="content()" />
        </section>
      } @else {
        <textarea
          nxField
          [attr.aria-label]="label()"
          [value]="content()"
          [readOnly]="disabled()"
          [attr.maxlength]="maxLength()"
          spellcheck="false"
          (input)="content.set($any($event.target).value)"
        ></textarea>
      }
    </div>`,
})
export class MarkdownEditor {
  readonly content = model.required<string>();
  readonly label = input('Markdown 內容');
  readonly disabled = input(false);
  readonly maxLength = input(64000);
  readonly view = signal('read');
  readonly views = [
    { value: 'read', label: '閱讀', icon: 'document' },
    { value: 'edit', label: '編輯', icon: 'edit' },
  ];
}
