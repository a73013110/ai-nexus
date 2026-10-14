import { CompactDialog } from './compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { DialogHeader } from './dialog-header';

export interface Confirmation {
  title: string;
  message: string;
  confirm: string;
  danger?: boolean;
}
let sequence = 0;
@Component({
  selector: 'nx-confirm-dialog',
  imports: [CompactDialog, DialogHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './confirm-dialog.scss',
  template: `<dialog
    [nxCompactDialog]="compact()"
    size="sm"
    #dialog
    class="platform-dialog"
    [attr.aria-label]="value()?.title"
    [attr.aria-describedby]="id + '-message'"
    (cancel)="$event.preventDefault(); answer(false)"
  >
    <nx-dialog-header [heading]="value()?.title ?? ''" closeLabel="關閉確認視窗" />
    <div class="dialog-body">
      <p class="confirmation-message" [id]="id + '-message'">{{ value()?.message }}</p>
    </div>
    <footer class="dialog-foot">
      <button type="button" autofocus class="secondary-button" (click)="answer(false)">取消</button>
      <button
        type="button"
        [class]="value()?.danger ? 'danger-button' : 'primary-button'"
        (click)="answer(true)"
      >
        {{ value()?.confirm }}
      </button>
    </footer>
  </dialog>`,
})
export class ConfirmDialog {
  readonly compact = input(true);
  readonly id = `nx-confirm-${++sequence}`;
  readonly value = signal<Confirmation | null>(null);
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private resolve?: (answer: boolean) => void;
  constructor() {
    inject(DestroyRef).onDestroy(() => this.resolve?.(false));
  }
  ask(value: Confirmation) {
    this.resolve?.(false);
    this.value.set(value);
    this.dialog().nativeElement.showModal();
    return new Promise<boolean>((resolve) => (this.resolve = resolve));
  }
  answer(value: boolean) {
    this.dialog().nativeElement.close();
    this.resolve?.(value);
    this.resolve = undefined;
  }
}
