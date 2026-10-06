import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { NotificationStore } from '../../core/notifications/notification-store';
import { notificationUrl } from '../../core/notifications/notification-target';
import { Icon } from './icon';
import { formatDate } from '../browser/format';

@Component({
  selector: 'nx-notification-center',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
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
    <div class="notification-toolbar">
      <button
        class="quiet-button"
        [attr.aria-pressed]="store.unreadOnly()"
        (click)="store.unreadOnly.update(toggle); store.refresh()"
      >
        只看未讀
      </button>
      <button
        class="quiet-button"
        [disabled]="!store.unread() || store.loading()"
        (click)="store.readAll()"
      >
        全部標為已讀
      </button>
      <button
        class="icon-button"
        aria-label="重新整理通知"
        [disabled]="store.loading()"
        (click)="store.refresh()"
      >
        <nx-icon name="refresh" />
      </button>
    </div>
    @if (store.error()) {
      <p class="error-banner" role="alert">
        {{ store.error() }}<button (click)="store.refresh()">重試</button>
      </p>
    }
    <div class="notification-list" [attr.aria-busy]="store.loading()">
      @for (item of store.items(); track item.id) {
        <article
          class="notification-row"
          [class.is-unread]="!item.readAt"
          [attr.data-severity]="item.severity"
        >
          <nx-icon
            [name]="
              item.severity === 'success' ? 'check' : item.severity === 'error' ? 'info' : 'bell'
            "
          />
          <div>
            <div class="notification-title">
              <strong>{{ item.title }}</strong>
              @if (!item.readAt) {
                <span class="notification-unread">未讀</span>
              }
            </div>
            <p>{{ item.body }}</p>
            <time [attr.datetime]="item.createdAt">{{ date(item.createdAt) }}</time>
            <div class="notification-actions">
              @if (url(item.target, item.version)) {
                <button class="quiet-button" (click)="store.activate(item)">
                  查看內容<nx-icon name="chevron" />
                </button>
              }
              @if (!item.readAt) {
                <button class="quiet-button" (click)="store.read(item).catch(ignore)">
                  標為已讀
                </button>
              }
            </div>
          </div>
          <button
            class="icon-button"
            [attr.aria-label]="'移除通知：' + item.title"
            (click)="store.dismiss(item)"
          >
            <nx-icon name="close" />
          </button>
        </article>
      } @empty {
        <div class="empty-state" role="status">
          <nx-icon name="bell" />
          <p>
            {{
              store.loading()
                ? '正在載入通知…'
                : store.unreadOnly()
                  ? '目前沒有未讀通知'
                  : '新的工作進度會出現在這裡'
            }}
          </p>
        </div>
      }
      @if (store.hasMore()) {
        <button class="secondary-button" [disabled]="store.loading()" (click)="store.refresh(true)">
          載入更早的通知
        </button>
      }
    </div>
  </dialog>`,
})
export class NotificationCenter {
  readonly store = inject(NotificationStore);
  readonly date = formatDate;
  readonly url = notificationUrl;
  readonly toggle = (value: boolean) => !value;
  readonly ignore = () => {};
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  constructor() {
    effect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.store.opened() && !dialog.open) dialog.showModal();
      else if (!this.store.opened() && dialog.open) dialog.close();
    });
  }
}
