import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { WorkspaceLayout } from '../../core/layout/workspace-layout';
import { NotificationStore } from './notification-store';
import { Icon } from '../../shared/ui/icon';
import { Tabs, type TabItem } from '../../shared/ui/tabs';
import { ToastHistory } from '../../shared/ui/toast-history';
import { ToastService } from '../../shared/ui/toast-service';
import { NotificationFeed } from './notification-feed';

/**
 * The server inbox, and beside it this tab's toasts: the inbox is what lasts, the session list
 * only helps find a confirmation that left before it was read.
 */
@Component({
  selector: 'nx-notification-center',
  imports: [Notice, CompactDialog, NotificationFeed, Icon, Tabs, ToastHistory],
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
    <nx-tabs
      #tabs
      label="通知來源"
      [items]="views()"
      [value]="store.view()"
      (valueChange)="store.view.set($any($event))"
    />
    <div
      role="tabpanel"
      class="notification-panel"
      [id]="tabs.panelId(store.view())"
      [attr.aria-labelledby]="tabs.tabId(store.view())"
    >
      @if (store.view() === 'session') {
        <nx-toast-history (activated)="leave()" />
      } @else {
        @defer (when store.opened()) {
          <nx-notification-feed />
        } @loading {
          <p class="form-note" role="status">正在載入通知...</p>
        } @error {
          <nx-notice message="通知介面暫時無法載入，請重新整理頁面。" />
        }
      }
    </div>
  </dialog>`,
})
export class NotificationCenter {
  readonly store = inject(NotificationStore);
  private readonly toasts = inject(ToastService);
  private readonly layout = inject(WorkspaceLayout);
  readonly views = computed<TabItem[]>(() => [
    { value: 'inbox', label: '通知匣' },
    { value: 'session', label: '本次工作階段', count: this.toasts.history().length },
  ]);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  constructor() {
    effect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.store.opened() && !dialog.open) dialog.showModal();
      else if (!this.store.opened() && dialog.open) dialog.close();
    });
  }
  /** A recorded action may navigate, so the dialog and the mobile sidebar get out of the way. */
  leave() {
    this.store.close();
    this.layout.closeMobile();
  }
}
