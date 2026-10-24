import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { uniqueId } from '@shared/browser/unique-id';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { Field } from '@shared/ui/field';
import { PendingButton } from '@shared/ui/pending-button';
import { AdminUserStore } from './admin-user-store';

/**
 * 帳號頁籤的「AD 綁定」段落：顯示是否已以 SID 綁定，並提供改綁與解除（ADR 0019：換 AD 帳號不算離職，沿用原本的使用者）。
 * 兩個操作都立即送出、各自確認，不經帳號表單的儲存；帳號表單有未儲存的變更時先停用，
 * 否則操作後重讀的資料會蓋掉草稿。
 */
@Component({
  selector: 'nx-directory-binding',
  imports: [ConfirmDialog, Field, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .binding-row {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      gap: var(--p-space-2) var(--p-space-3);
    }
    .binding-row > label {
      flex: 1 1 14rem;
      max-width: 24rem;
    }
  `,
  template: `
    <section class="form-section" role="group" [attr.aria-labelledby]="id + '-head'">
      <header class="form-section-head">
        <h3 [id]="id + '-head'">AD 綁定</h3>
        <p class="form-note">{{ status() }}</p>
      </header>
      <div class="form-section-body">
        <div class="binding-row">
          <label
            >{{ bound() ? '改綁到 AD 帳號' : '綁定 AD 帳號' }}（不含網域）<input
              nxField
              maxlength="64"
              autocomplete="off"
              [attr.aria-describedby]="id + '-hint'"
              [value]="account()"
              [readOnly]="blocked()"
              (input)="account.set($any($event.target).value)"
              (keydown.enter)="$event.preventDefault(); rebind()"
          /></label>
          <button
            nxPending
            type="button"
            class="secondary-button"
            [pending]="store.binding() === 'rebind'"
            [disabled]="blocked() || !account().trim()"
            (click)="rebind()"
          >
            {{ bound() ? '改綁' : '綁定' }}
          </button>
          @if (bound()) {
            <button
              nxPending
              type="button"
              class="danger-button"
              [pending]="store.binding() === 'unbind'"
              [disabled]="blocked()"
              (click)="unbind()"
            >
              解除綁定
            </button>
          }
        </div>
        <p [id]="id + '-hint'" class="form-note">
          @if (store.dirty().account) {
            請先儲存或還原上方的帳號變更。
          } @else {
            依 AD 短帳號查出 SID 後立即綁定，使用者 ID、角色與歷史資料沿用；目前的工作階段會失效。
          }
        </p>
      </div>
    </section>
    <nx-confirm-dialog />
  `,
})
export class DirectoryBinding {
  readonly store = inject(AdminUserStore);
  readonly id = uniqueId('nx-directory-binding');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly account = signal('');
  readonly bound = computed(() => !!this.store.current()?.authentication?.directoryBound);
  readonly blocked = computed(() => this.store.locked() || this.store.dirty().account);
  readonly status = computed(() => {
    const user = this.store.current(),
      adAccount = user?.authentication?.adAccount;
    if (this.bound()) return `已綁定：${user?.account ?? ''}`;
    return adAccount
      ? `尚未綁定：下次以 AD 帳號「${adAccount}」驗證或從 AD 匯入時綁定。`
      : '尚未綁定 AD 帳號。';
  });

  async rebind() {
    const user = this.store.current(),
      account = this.account().trim();
    if (!user || !account || this.blocked()) return;
    const confirmed = await this.confirmation().ask({
      title: `${this.bound() ? '改綁' : '綁定'} AD 帳號：${user.displayName}`,
      message: `改以 AD 帳號「${account}」登入此使用者，使用者 ID、角色與歷史資料沿用；原本綁定的 AD 帳號無法再登入此使用者。`,
      confirm: this.bound() ? '改綁' : '綁定',
    });
    if (confirmed && (await this.store.rebind(account))) this.account.set('');
  }

  async unbind() {
    const user = this.store.current();
    if (!user || this.blocked()) return;
    const adAccount = user.authentication?.adAccount ?? '';
    const confirmed = await this.confirmation().ask({
      title: `解除 AD 綁定：${user.displayName}`,
      message: `回到預建狀態，下次有人以 AD 帳號「${adAccount}」驗證或從 AD 匯入時綁定該帳號。用於 AD 重建同名帳號；換成不同帳號名請改用「改綁」。`,
      confirm: '解除綁定',
      danger: true,
    });
    if (confirmed) await this.store.unbind();
  }
}
