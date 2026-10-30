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
import { Router } from '@angular/router';
import type { AdminUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { formatDate } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Notice } from '@shared/ui/notice';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ToastService } from '@shared/ui/toast-service';
import { ViewMotion } from '@shared/ui/view-motion';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { AccessStatus } from '@features/admin/access-control/roles/access-status';
import { AdminUsersStore } from '../admin-users-store';
import { AccountReviewItems } from './account-review-items';
import { AdminUserConversations } from './admin-user-conversations';
import { AdminUserSettings } from './admin-user-settings';
import { AdminUserStore, type UserTab } from './admin-user-store';
import { UserConversationReader } from './user-conversation-reader';
import { signInMethods } from './user-account-fields';
import { routeOf } from '@core/api/catalog';
import { userInspectorTour } from '../identity-tour';

/**
 * 單一使用者的管理中心。標題列是紀錄樣式：第一行姓名、狀態與帳號，第二行角色、登入方式與最近活動；
 * 分頁是帳號、角色與權限、AI 模型、用量，以及稽核過的唯讀對話檢視。
 * 上下筆只在列表目前這一頁內移動，離開前與切換頁籤一樣確認未儲存的變更；
 * 複製使用者 ID 與活動稽核反查收在標題列的選單。
 */
@Component({
  selector: 'nx-admin-user-inspector',
  imports: [
    AccessStatus,
    AccountReviewItems,
    AdminUserConversations,
    ActionMenu,
    AdminUserSettings,
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DrawerPager,
    Notice,
    Tabs,
    ViewMotion,
  ],
  providers: [ViewScope, CopyFeedback, AdminUserStore, UserConversationReader],
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
  private readonly users = inject(AdminUsersStore);
  private readonly access = inject(AccessControlStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly clipboard = inject(CopyFeedback);
  readonly roles = computed(() => {
    const names = this.access.roleNames(this.store.current()?.roleIds ?? []);
    return names.length ? names.join('、') : '尚未分派角色';
  });
  readonly methods = signInMethods;
  readonly date = formatDate;
  readonly menu = computed<MenuAction[]>(() => [
    { id: 'copy-id', label: '複製使用者 ID', icon: 'copy' },
    ...(this.session.has('audit') ? [{ id: 'audit', label: '查閱活動稽核', icon: 'audit' }] : []),
  ]);
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
      { value: 'roles', label: '角色與權限', dirty: dirty.roles },
      { value: 'models', label: 'AI 模型', dirty: dirty.models },
      { value: 'usage', label: '用量', dirty: dirty.usage },
      { value: 'conversations', label: '對話' },
    ];
  });
  /** 「對話」頁籤改介紹受稽核的唯讀檢視，其他頁籤介紹標題列與頁籤。 */
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
  /** 離開前往其他頁面（稽核、帳號待辦）：不會詢問，對話框直接關閉。 */
  close() {
    this.dialog().nativeElement.close();
  }
  async act(action: string) {
    const user = this.store.current();
    if (!user) return;
    if (action === 'copy-id') {
      await this.clipboard.copy(user.id);
      if (this.clipboard.error())
        this.toast.show({ tone: 'warning', title: this.clipboard.error() });
      else this.toast.success('已複製使用者 ID');
    }
    if (action === 'audit') {
      this.close();
      void this.router.navigate([routeOf('audit')], { queryParams: { search: user.id } });
    }
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
