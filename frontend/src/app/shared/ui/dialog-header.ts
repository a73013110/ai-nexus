import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  inject,
  input,
} from '@angular/core';
import { requestDialogClose } from '../browser/dialog-close';
import { Icon } from './icon';

/** Title bar of a `.platform-dialog`: heading, optional detail and tools, and the close button. */
let headerSequence = 0;

@Component({
  selector: 'nx-dialog-header',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'dialog-bar' },
  template: `<div class="dialog-title">
      <h2 [id]="titleId">{{ heading() }}</h2>
      @if (detail()) {
        <small>{{ detail() }}</small>
      }
    </div>
    <ng-content />
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
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
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
