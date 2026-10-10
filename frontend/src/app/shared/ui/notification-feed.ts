import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationStore } from '../../core/notifications/notification-store';
import { notificationUrl } from '../../core/notifications/notification-target';
import { safeMessage, systemProblem, validIssueCode } from '../../core/api/safe-errors';
import type { NotificationDto } from '../../core/api/schema';
import { formatDate } from '../browser/format';
import { EmptyState } from './empty-state';
import { Notice } from './notice';
import { IssueCode } from './issue-code';
import { Icon } from './icon';

@Component({
  selector: 'nx-notification-feed',
  imports: [Notice, EmptyState, IssueCode, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: contents' },
  template: `<div class="notification-toolbar">
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
      <nx-notice [message]="store.error()"
        ><button notice-actions (click)="store.refresh()">重試</button>
      </nx-notice>
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
  readonly toggle = (value: boolean) => !value;
  readonly ignore = () => {};
}
