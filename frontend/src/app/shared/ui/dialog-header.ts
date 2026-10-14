import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  computed,
  inject,
  input,
} from '@angular/core';
import { requestDialogClose } from '../browser/dialog-close';
import { CompactDialog } from './compact-dialog';
import { Icon } from './icon';
import { idleChromePart } from './idle-chrome';

/**
 * Title bar of a `.platform-dialog`: heading, optional detail and tools, and the close button.
 * Content marked `dialog-title` (a status badge) sits beside the heading, outside the h2 so the
 * dialog's accessible name stays the heading alone; other content is placed before the close button.
 * `maximizable` adds the 放大視窗 toggle of `CompactDialog.maximized`; while maximized inside an
 * `nxIdleChrome` viewer the bar floats over the content and fades with its toolbar (idle-chrome.ts).
 */
let headerSequence = 0;

@Component({
  selector: 'nx-dialog-header',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'dialog-bar', '[attr.data-chrome]': 'chrome()' },
  template: `<div class="dialog-title">
      <h2 [id]="titleId">{{ heading() }}</h2>
      <ng-content select="[dialog-title]" />
      @if (detail()) {
        <small>{{ detail() }}</small>
      }
    </div>
    <ng-content />
    @if (maximizable() && dialog) {
      <button
        type="button"
        class="icon-button"
        [attr.aria-label]="maximized() ? '還原視窗' : '放大視窗'"
        [title]="maximized() ? '還原視窗' : '放大視窗'"
        (click)="dialog.maximized.set(!maximized())"
      >
        <nx-icon [name]="maximized() ? 'minimize' : 'maximize'" />
      </button>
    }
    @if (closable()) {
      <button
        type="button"
        class="icon-button"
        [attr.aria-label]="closeLabel()"
        title="關閉（Esc）"
        (click)="close()"
      >
        <nx-icon name="close" />
      </button>
    }`,
})
export class DialogHeader {
  readonly heading = input.required<string>();
  readonly detail = input('');
  readonly closable = input(true);
  readonly closeLabel = input('關閉');
  readonly maximizable = input(false, { transform: booleanAttribute });
  protected readonly dialog = inject(CompactDialog, { optional: true });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly maximized = computed(() => !!this.dialog?.maximized());
  protected readonly chrome = idleChromePart(this.host.nativeElement, this.maximized);
  protected readonly titleId = `nx-dialog-header-${++headerSequence}`;
  constructor() {
    // Content rendered after showModal() is missing at beforetoggle, so name the dialog here.
    afterNextRender(() => {
      const dialog = this.host.nativeElement.closest('dialog');
      if (dialog && !dialog.hasAttribute('aria-label'))
        dialog.setAttribute('aria-labelledby', this.titleId);
    });
  }
  close() {
    const dialog = this.host.nativeElement.closest('dialog');
    if (dialog) requestDialogClose(dialog);
  }
}
