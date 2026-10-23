import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { AdminUserDto, DirectoryOutcomeDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AdminUsersStore } from '../admin-users-store';
import { IdentityApi } from '../identity-api';
import { DirectoryOutcomeTable } from './directory-outcome-table';
import { DIRECTORY_BATCH_LIMIT, summarize } from './directory-outcomes';

/**
 * 批次「從 AD 更新」：從列表目前這一頁勾選使用者（預設勾有 AD 帳號的人），送出後在同一個對話框列出逐人結果。
 * AD 已停用或找不到的人只標示，是否停用平台帳號留給管理員在使用者詳情決定。
 */
@Component({
  selector: 'nx-directory-sync-dialog',
  imports: [Checkbox, CompactDialog, DialogHeader, DirectoryOutcomeTable, Notice, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .directory-sync-list {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: var(--p-space-1) var(--p-space-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    nx-directory-outcome-table {
      --table-max-height: min(52dvh, 30rem);
    }
  `,
  template: `
    <dialog
      nxCompactDialog
      size="lg"
      #dialog
      class="platform-dialog"
      (close)="opened.set(false)"
      [busy]="busy()"
    >
      <nx-dialog-header
        heading="從 AD 更新使用者"
        [detail]="outcomes() ? '更新結果' : '選取本頁使用者'"
        closeLabel="關閉從 AD 更新"
      />
      @if (opened()) {
        <div class="dialog-body">
          @if (error()) {
            <nx-notice tone="danger" [message]="error()" />
          }
          @if (outcomes(); as items) {
            <p class="form-note" role="status">{{ summary() }}</p>
            <nx-directory-outcome-table label="更新結果" [items]="items" />
          } @else {
            <p class="form-note">
              以 AD
              的姓名、通知信箱與帳號更新勾選的人，規則與登入時相同：管理員維護的姓名、本人或管理員設定的信箱不會被覆寫。AD
              已停用或找不到的人只標示，不會停用平台帳號。
            </p>
            <nx-checkbox
              label="全選本頁"
              [checked]="selected().size === users().length && users().length > 0"
              (checkedChange)="selectAll($event)"
            />
            <ul class="directory-sync-list" aria-label="本頁使用者">
              @for (user of users(); track user.id) {
                <li>
                  <nx-checkbox
                    [label]="user.displayName"
                    [description]="user.account"
                    [checked]="selected().has(user.id)"
                    (checkedChange)="toggle(user.id, $event)"
                  />
                </li>
              }
            </ul>
          }
        </div>
        <footer class="dialog-foot">
          @if (outcomes()) {
            <button type="button" class="primary-button" (click)="dialog.close()">完成</button>
          } @else {
            <p class="dialog-note">
              一次最多 {{ limit }} 人；沒有綁定 AD 的人會標示「未綁定 AD」。
            </p>
            <button
              type="button"
              class="secondary-button"
              [attr.aria-disabled]="busy() || null"
              (click)="busy() || dialog.close()"
            >
              取消
            </button>
            <button
              nxPending
              type="button"
              class="primary-button"
              [pending]="busy()"
              [disabled]="!selected().size"
              (click)="sync()"
            >
              {{ busy() ? '更新中…' : '從 AD 更新（' + selected().size + ' 人）' }}
            </button>
          }
        </footer>
      }
    </dialog>
  `,
})
export class DirectorySyncDialog {
  private readonly api = inject(IdentityApi);
  private readonly store = inject(AdminUsersStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly limit = DIRECTORY_BATCH_LIMIT;
  readonly opened = signal(false);
  readonly users = signal<AdminUserDto[]>([]);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly outcomes = signal<DirectoryOutcomeDto[] | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly summary = computed(() => summarize(this.outcomes() ?? []));

  /** 列表一頁 100 筆，剛好是伺服器的單次上限；預設勾選有 AD 帳號的人。 */
  open(users: readonly AdminUserDto[]) {
    const page = users.slice(0, this.limit);
    this.users.set(page);
    this.selected.set(
      new Set(page.filter((user) => user.authentication?.adAccount).map((user) => user.id)),
    );
    this.outcomes.set(null);
    this.error.set('');
    this.opened.set(true);
    this.dialog().nativeElement.showModal();
  }
  toggle(id: string, checked: boolean) {
    const next = new Set(this.selected());
    if (checked) next.add(id);
    else next.delete(id);
    this.selected.set(next);
  }
  selectAll(checked: boolean) {
    this.selected.set(new Set(checked ? this.users().map((user) => user.id) : []));
  }
  async sync() {
    if (!this.selected().size || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const ids = this.users()
        .map((user) => user.id)
        .filter((id) => this.selected().has(id));
      this.outcomes.set((await this.api.syncDirectoryUsers(ids)).items);
      this.store.readUsers();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
