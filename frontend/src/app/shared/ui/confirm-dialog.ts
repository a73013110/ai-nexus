import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';

export interface Confirmation {
  title: string;
  message: string;
  confirm: string;
  danger?: boolean;
}
@Component({
  selector: 'nx-confirm-dialog',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    #dialog
    class="platform-dialog"
    (cancel)="$event.preventDefault(); answer(false)"
  >
    <div class="dialog-heading">
      <h2>{{ value()?.title }}</h2>
      <button class="icon-button" aria-label="關閉確認視窗" (click)="answer(false)">
        <nx-icon name="close" />
      </button>
    </div>
    <p class="confirmation-message">{{ value()?.message }}</p>
    <div class="dialog-actions">
      <button autofocus class="secondary-button" (click)="answer(false)">取消</button
      ><button
        [class]="value()?.danger ? 'danger-button' : 'primary-button'"
        (click)="answer(true)"
      >
        {{ value()?.confirm }}
      </button>
    </div>
  </dialog>`,
})
export class ConfirmDialog {
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
