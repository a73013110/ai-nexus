import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationStore } from './notification-store';
import { notificationUrl } from './notification-target';
import { safeMessage, systemProblem, validIssueCode } from '@core/errors/safe-errors';
import type { NotificationDto } from '@core/api/schema';
import { formatDate } from '@shared/browser/format';
import { EmptyState } from '@shared/ui/empty-state';
import { Notice } from '@shared/ui/notice';
import { IssueCode } from '@shared/ui/issue-code';
import { Icon } from '@shared/ui/icon';
import { PendingButton } from '@shared/ui/pending-button';

@Component({
  selector: 'nx-notification-feed',
  imports: [Notice, EmptyState, IssueCode, Icon, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './notification-feed.scss',
  host: { style: 'display: contents' },
  template: `@if (store.error()) {
      <nx-notice [message]="store.error()"
        ><button
          notice-actions
          nxPending
          type="button"
          [pending]="store.loading()"
          (click)="store.refresh()"
        >
          {{ store.loading() ? '重試中…' : '重試' }}
        </button>
      </nx-notice>
    }
    <div
      class="notification-list"
      [class.is-refreshing]="store.loading() && store.items().length > 0"
      [attr.aria-busy]="store.loading() || null"
    >
      @if (store.loading() && !store.items().length) {
        <div class="notification-skeleton" role="status">
          @for (width of skeleton; track $index) {
            <span class="skeleton" [style.width.%]="width"></span>
          }
          <span class="sr-only">正在載入通知…</span>
        </div>
      }
      @for (item of store.items(); track item.id) {
        @let destination = url(item.target, item.version);
        @let text = body(item);
        <!-- 整列只是「查看內容」的滑鼠捷徑，鍵盤仍走該按鈕。 -->
        <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
        <article
          class="notification-row"
          [class.is-unread]="!item.readAt"
          [class.is-actionable]="destination"
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
            <p>{{ text }}<nx-issue-code [message]="text" /></p>
            <div class="notification-actions">
              <time [attr.datetime]="item.createdAt">{{ date(item.createdAt) }}</time>
              @if (destination) {
                <button type="button" class="quiet-button" (click)="store.activate(item)">
                  查看內容<nx-icon name="chevron" />
                </button>
              }
              @if (!item.readAt) {
                @let reading = store.isWorking('read:' + item.id);
                <button
                  nxPending
                  type="button"
                  class="quiet-button"
                  [pending]="reading"
                  (click)="store.read(item).catch(ignore)"
                >
                  {{ reading ? '標記中…' : '標為已讀' }}
                </button>
              }
            </div>
          </div>
          <button
            nxPending
            type="button"
            class="icon-button"
            [attr.aria-label]="'移除通知：' + item.title"
            [pending]="store.isWorking('dismiss:' + item.id)"
            (click)="store.dismiss(item)"
          >
            <nx-icon name="close" />
          </button>
        </article>
      } @empty {
        <!-- 讀取中或讀取失敗時還不知道有沒有通知，不先說「沒有」。 -->
        @if (!store.loading() && !store.error()) {
          <nx-empty-state>
            <nx-icon name="bell" />
            <p>{{ store.unreadOnly() ? '目前沒有未讀通知' : '新的工作進度會出現在這裡' }}</p>
          </nx-empty-state>
        }
      }
      @if (store.hasMore()) {
        <button
          nxPending
          type="button"
          class="secondary-button"
          [pending]="store.loadingMore()"
          (click)="store.loading() || store.refresh(true)"
        >
          {{ store.loadingMore() ? '載入中…' : '載入更早的通知' }}
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
  /** 骨架列的寬度（%），長短交錯才像通知。 */
  protected readonly skeleton = [82, 58, 74, 50, 66];
  /** 點擊列自己的控制項與文字選取，保留其原本行為。 */
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
