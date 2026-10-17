import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { MarkdownView } from './markdown-view';
import { Field } from '../ui/field';
import { ViewSwitch } from '../ui/view-switch';
import { ViewMotion } from '../ui/view-motion';

/** 單一文件的值，有可讀的預設值與明確的 Markdown 編輯模式。 */
@Component({
  selector: 'nx-markdown-editor',
  imports: [MarkdownView, Field, ViewSwitch, ViewMotion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './markdown-editor.scss',
  template: `@if (showModeSwitch()) {
      <div class="markdown-editor-toolbar">
        <strong>{{ label() }}</strong>
        <nx-view-switch
          [label]="label() + '呈現方式'"
          appearance="segment"
          [options]="views"
          [value]="mode()"
          (valueChange)="mode.set($any($event))"
        />
        <ng-content select="[editor-actions]" />
      </div>
    }
    <div class="markdown-editor-panel" [nxViewMotion]="mode()">
      @if (mode() === 'read') {
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
  /** 閱讀或編輯；綁定它即可從外部切換。 */
  readonly mode = model<'read' | 'edit'>('read');
  /** 宿主自行渲染切換鈕時為 false。 */
  readonly showModeSwitch = input(true);
  readonly views = [
    { value: 'read', label: '閱讀', icon: 'document' },
    { value: 'edit', label: '編輯', icon: 'edit' },
  ];
}
