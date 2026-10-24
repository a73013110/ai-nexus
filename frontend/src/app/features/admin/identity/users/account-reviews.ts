import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import type { AccountReviewUserDto } from '@core/api/schema';
import { CountBadge } from '@shared/ui/count-badge';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { AccessStatus } from '@features/admin/access-control/roles/access-status';
import { AdminUsersStore } from '../admin-users-store';
import { AccountReviewItems } from './account-review-items';

/**
 * 使用者頁工具列的「帳號待辦」與它開啟的面板：停用或復職後需要管理員判斷的事，依人分組（ADR 0019）。
 * 面板在桌面不擋住列表，點名字以帳號搜尋列表，再從列表開啟使用者管理。
 */
@Component({
  selector: 'nx-account-reviews',
  imports: [AccessStatus, AccountReviewItems, CountBadge, DetailDrawer, EmptyState, Icon, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .account-reviews {
      display: grid;
      gap: var(--p-space-4);
    }
    section {
      display: grid;
      gap: var(--p-space-2);
      padding-bottom: var(--p-space-4);
      border-bottom: 1px solid var(--line);
    }
    section:last-child {
      padding-bottom: 0;
      border-bottom: 0;
    }
    header {
      display: flex;
      flex-wrap: wrap;
      gap: var(--p-space-2);
      align-items: center;
    }
    header small {
      color: var(--muted);
    }
  `,
  template: `<button
      type="button"
      class="secondary-button account-reviews-button"
      [attr.aria-label]="label()"
      [title]="label()"
      (click)="drawer().open()"
    >
      <nx-icon name="tasks" />帳號待辦<nx-count-badge [count]="users().length" tone="warning" />
    </button>
    <nx-detail-drawer title="帳號待辦">
      <div
        class="account-reviews"
        [class.is-refreshing]="store.reviewsRead.refreshing() && !!store.reviews()"
        [attr.aria-busy]="store.reviewsRead.refreshing() || null"
      >
        @if (store.reviewsRead.error()) {
          <nx-notice [message]="store.reviewsRead.error()"
            ><button notice-actions type="button" (click)="store.reviewsRead.reload()">
              重新載入
            </button></nx-notice
          >
        }
        @for (notice of store.reviews()?.notices ?? []; track notice) {
          <nx-notice tone="warning" [message]="notice" />
        }
        @for (user of users(); track user.id) {
          <section [attr.aria-label]="user.displayName">
            <header>
              @if (user.deleted) {
                <strong>{{ user.displayName }}</strong>
                <small>已刪除</small>
              } @else {
                <button
                  type="button"
                  class="quiet-button"
                  title="在列表中搜尋這位使用者"
                  (click)="store.find(user.account)"
                >
                  <nx-icon name="search" />{{ user.displayName }}
                </button>
                <nx-access-status [enabled]="user.enabled" />
              }
              <small>{{ user.account }}</small>
            </header>
            <nx-account-review-items [items]="user.items" (navigate)="drawer().close()" />
          </section>
        } @empty {
          @if (store.reviews()) {
            <nx-empty-state><p>目前沒有帳號待辦。</p></nx-empty-state>
          } @else if (!store.reviewsRead.error()) {
            <span class="skeleton" role="status" aria-label="正在載入帳號待辦…"></span>
          }
        }
      </div>
    </nx-detail-drawer>`,
})
export class AccountReviews {
  readonly store = inject(AdminUsersStore);
  readonly drawer = viewChild.required(DetailDrawer);
  readonly users = computed<readonly AccountReviewUserDto[]>(
    () => this.store.reviews()?.users ?? [],
  );
  readonly label = computed(() => {
    const count = this.users().length;
    return count ? `帳號待辦：${count} 位使用者` : '帳號待辦';
  });
}
