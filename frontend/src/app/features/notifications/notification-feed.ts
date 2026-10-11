import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationStore } from './notification-store';
import { notificationUrl } from './notification-target';
import { safeMessage, systemProblem, validIssueCode } from '../../core/errors/safe-errors';
import type { NotificationDto } from '../../core/api/schema';
import { formatDate } from '../../shared/browser/format';
import { EmptyState } from '../../shared/ui/empty-state';
import { Notice } from '../../shared/ui/notice';
import { IssueCode } from '../../shared/ui/issue-code';
import { Icon } from '../../shared/ui/icon';

@Component({
  selector: 'nx-notification-feed',
  imports: [Notice, EmptyState, IssueCode, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './notification-feed.scss',
  host: { style: 'display: contents' },
  template: `@if (store.error()) {
      <nx-notice [message]="store.error()"
        ><button notice-actions (click)="store.refresh()">重試</button>
      </nx-notice>
    }
    <div class="notification-list" [attr.aria-busy]="store.loading()">
      @for (item of store.items(); track item.id) {
        <!-- The row is a pointer shortcut for 「查看內容」, which stays the keyboard path. -->
        <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
        <article
          class="notification-row"
          [class.is-unread]="!item.readAt"
          [class.is-actionable]="url(item.target, item.version)"
          [attr.data-severity]="item.severity"
          (click)="open($event, item)"
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
            <p>{{ body(item) }}<nx-issue-code [message]="body(item)" /></p>
            <div class="notification-actions">
              <time [attr.datetime]="item.createdAt">{{ date(item.createdAt) }}</time>
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
        <nx-empty-state role="status">
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
        </nx-empty-state>
      }
      @if (store.hasMore()) {
        <button class="secondary-button" [disabled]="store.loading()" (click)="store.refresh(true)">
          載入更早的通知
        </button>
      }
    </div>`,
})
export class NotificationFeed {
  body(item: NotificationDto) {
    return item.severity === 'error'
      ? validIssueCode(item.issueCode)
        ? systemProblem(item.issueCode)
        : safeMessage(item)
      : item.body;
  }
  readonly store = inject(NotificationStore);
  readonly date = formatDate;
  readonly url = notificationUrl;
  readonly ignore = () => {};
  /** Clicks on the row's own controls and text selection keep their behavior. */
  open(event: MouseEvent, item: NotificationDto) {
    if (
      !this.url(item.target, item.version) ||
      !(event.target instanceof Element) ||
      event.target.closest('button,a,input,select,textarea') ||
      window.getSelection()?.toString()
    )
      return;
    void this.store.activate(item);
  }
}
