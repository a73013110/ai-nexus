import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AdminUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Icon } from '@shared/ui/icon';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { AccessStatus } from '../roles/access-status';
import { AdminUserConversations } from './admin-user-conversations';
import { AdminUserSettings } from './admin-user-settings';
import { AdminUserStore, type UserTab } from './admin-user-store';
import { AdminUserSummary } from './admin-user-summary';
import { UserConversationReader } from './user-conversation-reader';

/**
 * One user's management centre: who they are and their last 30 days above tabs for the account,
 * roles, model policy, storage and an audited read-only view of their conversations. The shared
 * title bar names the dialog after the user; the summary and the tabs follow it.
 * 標題列的上下筆只在列表目前這一頁內移動，離開前與切換頁籤一樣確認未儲存的變更；
 * 使用者 ID 與活動稽核反查放在摘要的關聯列。
 */
@Component({
  selector: 'nx-admin-user-inspector',
  imports: [
    AccessStatus,
    AdminUserConversations,
    AdminUserSettings,
    AdminUserSummary,
    CompactDialog,
    ConfirmDialog,
    CopyValue,
    DialogHeader,
    DrawerPager,
    Icon,
    RouterLink,
    Tabs,
    ViewMotion,
  ],
  providers: [ViewScope, AdminUserStore, UserConversationReader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-user-inspector.html',
  styleUrl: './admin-user-inspector.scss',
})
export class AdminUserInspector {
  readonly user = input<AdminUserDto | null>(null);
  /** 開啟中的使用者在列表目前這一頁的位置；不在這一頁時為 -1。 */
  readonly index = input(-1);
  readonly count = input(0);
  readonly closed = output<void>();
  readonly move = output<-1 | 1>();
  readonly store = inject(AdminUserStore);
  readonly session = inject(WorkspaceSession);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  /** Each settings tab saves on its own and marks its unsaved changes. */
  readonly tabs = computed<readonly (TabItem & { value: UserTab })[]>(() => {
    const dirty = this.store.dirty();
    return [
      { value: 'account', label: '帳號', dirty: dirty.account },
      { value: 'roles', label: '角色與權限', dirty: dirty.roles },
      { value: 'models', label: 'AI 模型', dirty: dirty.models },
      { value: 'storage', label: '附件容量', dirty: dirty.storage },
      { value: 'conversations', label: '對話' },
    ];
  });
  constructor() {
    effect(() => {
      const user = this.user(),
        dialog = this.dialog().nativeElement;
      untracked(() => {
        if (user && (!dialog.open || user.id !== this.store.user()?.id)) {
          this.store.open(user);
          if (!dialog.open) dialog.showModal();
        } else if (!user && dialog.open) dialog.close();
      });
    });
  }
  /** The content stays for the exit, then the user's details leave the page with it. */
  async closing() {
    this.closed.emit();
    const dialog = this.dialog().nativeElement;
    await new Promise((done) => requestAnimationFrame(done));
    await Promise.allSettled(dialog.getAnimations({ subtree: true }).map((x) => x.finished));
    if (!dialog.open) this.store.release();
  }
  /** Leaving for the audit page: nothing is asked, the dialog simply goes. */
  close() {
    this.dialog().nativeElement.close();
  }
  async requestClose() {
    if (await this.mayLeave()) this.close();
  }
  /** Escape, the close button and the sheet's swipe arrive here; unsaved changes are confirmed. */
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async step(direction: -1 | 1) {
    if (await this.mayLeave()) {
      const tab = this.store.tab();
      if (tab !== 'conversations') this.store.revert(tab);
      this.move.emit(direction);
    }
  }
  async selectTab(value: string) {
    const tab = value as UserTab,
      from = this.store.tab();
    if (tab === from || !(await this.mayLeave())) return;
    if (from !== 'conversations') this.store.revert(from);
    this.store.select(tab);
  }
  private async mayLeave() {
    const tab = this.store.tab();
    if (this.store.saving()) return false;
    if (tab === 'conversations' || !this.store.dirty()[tab]) return true;
    return this.confirmation().ask({
      title: '放棄未儲存的變更？',
      message: `「${this.tabs().find((x) => x.value === tab)?.label}」的變更尚未儲存，離開後會恢復為已儲存的設定。`,
      confirm: '放棄變更',
    });
  }
}
