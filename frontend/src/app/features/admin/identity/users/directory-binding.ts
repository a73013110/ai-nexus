import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { uniqueId } from '@shared/browser/unique-id';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { Field } from '@shared/ui/field';
import { PendingButton } from '@shared/ui/pending-button';
import { AdminUserStore } from './admin-user-store';

/**
 * 「AD 驗證」下方的綁定狀態，投影進 `UserAccountFields` 的 `[ad-binding]`（ADR 0019：換 AD 帳號不算離職，沿用原本的使用者）。
 * 已綁定時是一列 `.form-choice`：「改綁」在原處換成輸入框，「解除綁定」收在選單；尚未綁定時可依已儲存的帳號立即綁定。
 * 這些動作立即送出、各自確認，不經帳號表單的儲存；帳號表單有未儲存的變更時先停用，
 * 否則操作後重讀的資料會蓋掉草稿。
 */
@Component({
  selector: 'nx-directory-binding',
  imports: [ActionMenu, ConfirmDialog, Field, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .binding-actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--p-space-2);
    }
  `,
  template: `
    @if (bound() && !editing()) {
      <div class="form-choice">
        <span class="form-choice-text"
          ><strong>{{ store.current()?.account }}</strong
          ><small>已以 SID 綁定；AD 改名請用「改綁」。</small></span
        >
        <button
          type="button"
          class="secondary-button"
          [disabled]="blocked()"
          (click)="editing.set(true)"
        >
          改綁
        </button>
        <nx-action-menu
          label="AD 綁定的其他操作"
          [items]="menu()"
          [busy]="store.acting() === 'unbind'"
          (action)="unbind()"
        />
      </div>
    } @else if (editing()) {
      <label
        >改綁到 AD 帳號（不含網域）<input
          nxField
          maxlength="64"
          autocomplete="off"
          [attr.aria-describedby]="id + '-hint'"
          [value]="account()"
          [readOnly]="blocked()"
          (input)="account.set($any($event.target).value)"
          (keydown.enter)="$event.preventDefault(); rebind(account())"
      /></label>
      <div class="binding-actions">
        <button type="button" class="secondary-button" [disabled]="busy()" (click)="cancel()">
          取消
        </button>
        <button
          nxPending
          type="button"
          class="secondary-button"
          [pending]="store.acting() === 'rebind'"
          [disabled]="blocked() || !account().trim()"
          (click)="rebind(account())"
        >
          確認改綁
        </button>
      </div>
    } @else if (savedAccount()) {
      <div class="binding-actions">
        <button
          nxPending
          type="button"
          class="secondary-button"
          [attr.aria-describedby]="id + '-hint'"
          [pending]="store.acting() === 'rebind'"
          [disabled]="blocked()"
          (click)="rebind(savedAccount())"
        >
          立即綁定
        </button>
      </div>
    }
    <p [id]="id + '-hint'" class="form-note">{{ hint() }}</p>
    <nx-confirm-dialog />
  `,
})
export class DirectoryBinding {
  readonly store = inject(AdminUserStore);
  readonly id = uniqueId('nx-directory-binding');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editing = signal(false);
  readonly account = signal('');
  readonly bound = computed(() => !!this.store.current()?.authentication?.directoryBound);
  /** 已儲存的 AD 帳號；尚未綁定時「立即綁定」用它查 SID。 */
  readonly savedAccount = computed(() => this.store.current()?.authentication?.adAccount ?? '');
  readonly busy = computed(() => this.store.locked());
  readonly blocked = computed(() => this.busy() || this.store.dirty().account);
  readonly menu = computed<MenuAction[]>(() => [
    { id: 'unbind', label: '解除綁定', icon: 'close', danger: true, disabled: this.blocked() },
  ]);
  readonly hint = computed(() => {
    if (this.store.dirty().account) return '請先儲存或還原帳號變更，再調整 AD 綁定。';
    if (this.editing())
      return '依 AD 短帳號查出 SID 後立即綁定，使用者 ID、角色與歷史資料沿用；目前的工作階段會失效。';
    if (this.bound()) return '';
    return this.savedAccount()
      ? `尚未綁定：下次以「${this.savedAccount()}」驗證或從 AD 匯入時綁定，也可以立即綁定。`
      : '儲存 AD 帳號後，下次驗證或從 AD 匯入時綁定。';
  });

  cancel() {
    this.editing.set(false);
    this.account.set('');
  }

  async rebind(value: string) {
    const user = this.store.current(),
      account = value.trim();
    if (!user || !account || this.blocked()) return;
    const confirmed = await this.confirmation().ask({
      title: `${this.bound() ? '改綁' : '綁定'} AD 帳號：${user.displayName}`,
      message: `改以 AD 帳號「${account}」登入此使用者，使用者 ID、角色與歷史資料沿用；原本綁定的 AD 帳號無法再登入此使用者。`,
      confirm: this.bound() ? '改綁' : '綁定',
    });
    if (confirmed && (await this.store.rebind(account))) this.cancel();
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
