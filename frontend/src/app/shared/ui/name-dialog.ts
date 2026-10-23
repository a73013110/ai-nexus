import { Notice } from './notice';
import { CompactDialog } from './compact-dialog';
import { DialogHeader } from './dialog-header';
import { Field } from './field';
import { PendingButton } from './pending-button';
import { safeMessage } from '@core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  signal,
  viewChild,
} from '@angular/core';

export interface NameRequest {
  title: string;
  value: string;
  maxLength?: number;
  description?: string;
  save: (name: string) => Promise<void>;
}
@Component({
  imports: [Notice, CompactDialog, DialogHeader, Field, PendingButton],
  selector: 'nx-name-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    [nxCompactDialog]="compact()"
    size="sm"
    #dialog
    class="platform-dialog"
    [attr.aria-label]="request()?.title"
    [busy]="busy()"
  >
    <form class="dialog-form" (submit)="save($event)">
      <nx-dialog-header [heading]="request()?.title ?? ''" />
      <div class="dialog-body">
        @if (request()?.description) {
          <p class="form-note">{{ request()?.description }}</p>
        }
        <label
          >名稱<input
            nxField
            #field
            required
            [value]="name()"
            [maxLength]="request()?.maxLength || 120"
            [readOnly]="busy()"
            (input)="name.set($any($event.target).value)"
        /></label>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
      </div>
      <footer class="dialog-foot">
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="busy() || null"
          (click)="busy() || dialog.close()"
        >
          取消
        </button>
        <button nxPending class="primary-button" [pending]="busy()" [disabled]="!name().trim()">
          {{ busy() ? '儲存中…' : '儲存名稱' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class NameDialog {
  readonly compact = input(true);
  readonly request = signal<NameRequest | null>(null);
  readonly name = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  open(request: NameRequest) {
    this.request.set(request);
    this.name.set(request.value);
    this.error.set('');
    this.dialog().nativeElement.showModal();
    requestAnimationFrame(() => {
      const field = this.field().nativeElement;
      field.focus();
      const dot = request.value.lastIndexOf('.');
      field.setSelectionRange(0, dot > 0 ? dot : request.value.length);
    });
  }
  async save(event: Event) {
    event.preventDefault();
    const request = this.request();
    if (!request || this.busy() || !this.name().trim()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await request.save(this.name().trim());
      this.dialog().nativeElement.close();
    } catch (error2) {
      this.error.set(safeMessage(error2));
    } finally {
      this.busy.set(false);
    }
  }
}
