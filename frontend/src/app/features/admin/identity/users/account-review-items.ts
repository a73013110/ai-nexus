import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type { AccountReviewTaskDto } from '@core/api/schema';
import { Icon } from '@shared/ui/icon';

/**
 * 一位使用者的帳號待辦，帳號待辦面板與使用者管理共用。`route` 由後端以功能目錄推得並附查詢字串，
 * 檢視者沒有處理頁的功能時為 null，只顯示說明。
 */
@Component({
  selector: 'nx-account-review-items',
  imports: [Icon, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    ul {
      display: grid;
      gap: var(--p-space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: flex;
      gap: var(--p-space-2);
      align-items: flex-start;
      justify-content: space-between;
    }
    strong {
      display: block;
      font-size: var(--p-font-sm);
      font-weight: 600;
    }
    p {
      margin: 0.125rem 0 0;
      color: var(--muted);
      font-size: var(--p-font-sm);
      overflow-wrap: anywhere;
    }
    a {
      flex: none;
    }
  `,
  template: `<ul>
    @for (item of items(); track item.code) {
      <li>
        <div>
          <strong>{{ item.title }}</strong>
          @if (item.detail) {
            <p>{{ item.detail }}</p>
          }
        </div>
        @if (item.route) {
          <a
            class="quiet-button"
            [routerLink]="router.parseUrl(item.route)"
            (click)="navigate.emit()"
            >前往處理<nx-icon name="chevron"
          /></a>
        }
      </li>
    }
  </ul>`,
})
export class AccountReviewItems {
  readonly router = inject(Router);
  readonly items = input.required<readonly AccountReviewTaskDto[]>();
  /** 連結離開本頁前，讓所在的對話框或面板先關閉。 */
  readonly navigate = output<void>();
}
