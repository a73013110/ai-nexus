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
import { Notice } from '@shared/ui/notice';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { AccessStatus } from '@features/admin/access-control/roles/access-status';
import { AdminUsersStore } from '../admin-users-store';
import { AccountReviewItems } from './account-review-items';
import { AdminUserConversations } from './admin-user-conversations';
import { AdminUserSettings } from './admin-user-settings';
import { AdminUserStore, type UserTab } from './admin-user-store';
import { AdminUserSummary } from './admin-user-summary';
import { UserConversationReader } from './user-conversation-reader';
import { routeOf } from '@core/api/catalog';
import { userInspectorTour } from '../identity-tour';

/**
 * 單一使用者的管理中心：他是誰，以及其最近 30 天的資料；分頁包括帳號、
 * 角色、模型政策、儲存空間，以及稽核過的唯讀對話檢視。共用的標題列以使用者名稱為對話框命名；
 * 摘要與分頁隨之變化。
 * 標題列的上下筆只在列表目前這一頁內移動，離開前與切換頁籤一樣確認未儲存的變更；
 * 使用者 ID 與活動稽核反查放在摘要的關聯列。
 */
@Component({
  selector: 'nx-admin-user-inspector',
  imports: [
    AccessStatus,
    AccountReviewItems,
    AdminUserConversations,
    AdminUserSettings,
    AdminUserSummary,
    CompactDialog,
    ConfirmDialog,
    CopyValue,
    DialogHeader,
    DrawerPager,
    Icon,
    Notice,
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
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly user = input<AdminUserDto | null>(null);
  /** 開啟中的使用者在列表目前這一頁的位置；不在這一頁時為 -1。 */
  readonly index = input(-1);
  readonly count = input(0);
  readonly closed = output<void>();
  readonly move = output<-1 | 1>();
  readonly store = inject(AdminUserStore);
  readonly session = inject(WorkspaceSession);
  private readonly users = inject(AdminUsersStore);
  /** 帳號待辦來自頁面的列表 store，處理後隨列表重讀更新。 */
  readonly reviews = computed(() => {
    const id = this.store.current()?.id;
    return id ? this.users.reviewsOf(id) : [];
  });
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  /** 每個設定分頁各自儲存，並標示其未儲存的變更。 */
  readonly tabs = computed<readonly (TabItem & { value: UserTab })[]>(() => {
    const dirty = this.store.dirty();
    return [
      { value: 'account', label: '帳號', dirty: dirty.account },
      { value: 'email', label: '通知信箱', dirty: dirty.email },
      { value: 'roles', label: '角色與權限', dirty: dirty.roles },
      { value: 'models', label: 'AI 模型', dirty: dirty.models },
      { value: 'storage', label: '附件容量', dirty: dirty.storage },
      { value: 'conversations', label: '對話' },
    ];
  });
  /** 「對話」頁籤改介紹受稽核的唯讀檢視，其他頁籤介紹摘要與頁籤。 */
  readonly tour = computed(() =>
    userInspectorTour({
      audit: this.session.has('audit'),
      conversations: this.store.tab() === 'conversations',
    }),
  );
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
  /** 內容在退場期間保留，之後使用者的詳情才一併離開頁面。 */
  async closing() {
    this.closed.emit();
    const dialog = this.dialog().nativeElement;
    await new Promise((done) => requestAnimationFrame(done));
    await Promise.allSettled(dialog.getAnimations({ subtree: true }).map((item) => item.finished));
    if (!dialog.open) this.store.release();
  }
  /** 離開前往稽核頁面：不會詢問，對話框直接關閉。 */
  close() {
    this.dialog().nativeElement.close();
  }
  async requestClose() {
    if (await this.mayLeave()) this.close();
  }
  /** Escape、關閉鈕與抽屜的滑動都會到這裡；未儲存的變更會先確認。 */
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
      message: `「${this.tabs().find((item) => item.value === tab)?.label}」的變更尚未儲存，離開後會恢復為已儲存的設定。`,
      confirm: '放棄變更',
    });
  }
}
