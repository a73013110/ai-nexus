import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { NotificationStore } from './notification-store';
import { Icon } from '../../shared/ui/icon';
import { NotificationFeed } from './notification-feed';

@Component({
  selector: 'nx-notification-center',
  imports: [Notice, CompactDialog, NotificationFeed, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './notification-center.scss',
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog notification-center"
    aria-labelledby="notification-title"
    (cancel)="store.close()"
    (close)="store.close()"
  >
    <header class="dialog-heading">
      <div>
        <h2 id="notification-title">通知</h2>
        <p class="form-note">{{ store.unread() }} 則未讀通知</p>
      </div>
      <button
        autofocus
        type="button"
        class="icon-button"
        aria-label="關閉通知"
        (click)="store.close()"
      >
        <nx-icon name="close" />
      </button>
    </header>
    @defer (when store.opened()) {
      <nx-notification-feed />
    } @loading {
      <p class="form-note" role="status">正在載入通知...</p>
    } @error {
      <nx-notice message="通知介面暫時無法載入，請重新整理頁面。" />
    }
  </dialog>`,
})
export class NotificationCenter {
  readonly store = inject(NotificationStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  constructor() {
    effect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.store.opened() && !dialog.open) dialog.showModal();
      else if (!this.store.opened() && dialog.open) dialog.close();
    });
  }
}
