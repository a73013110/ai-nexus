import { IssueCode } from './issue-code';
import { safeMessage } from '../../core/api/safe-errors';
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
  imports: [IssueCode],
  selector: 'nx-name-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    #dialog
    class="workspace-dialog"
    [class.ui-dialog-compact]="compact()"
    [class.ui-density-compact]="compact()"
    [attr.aria-label]="request()?.title"
    (cancel)="cancel($event)"
  >
    <form class="platform-form dialog-scroll" (submit)="save($event)">
      <h2>{{ request()?.title }}</h2>
      @if (request()?.description) {
        <p class="form-note">{{ request()?.description }}</p>
      }
      <label
        >名稱<input
          #field
          required
          [value]="name()"
          [maxLength]="request()?.maxLength || 120"
          [readOnly]="busy()"
          (input)="name.set($any($event.target).value)"
      /></label>
      @if (error()) {
        <p class="error-banner" role="alert">{{ error() }}<nx-issue-code [message]="error()" /></p>
      }
      <div class="dialog-actions">
        <button type="button" class="secondary-button" [disabled]="busy()" (click)="dialog.close()">
          取消
        </button>
        <button class="primary-button" [disabled]="busy() || !name().trim()">
          {{ busy() ? '正在儲存…' : '儲存名稱' }}
        </button>
      </div>
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
  cancel(event: Event) {
    if (this.busy()) event.preventDefault();
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
    } catch (e) {
      this.error.set(safeMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
