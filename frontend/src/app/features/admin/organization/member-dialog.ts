import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { UnitCandidateDto, UnitDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { PersonBrowser, type BrowsePerson } from '@shared/ui/person-browser';
import { OrganizationApi } from './organization-api';
import { OrganizationStore } from './organization-store';
import { unitRows } from './unit-tree';

const UNASSIGNED = '尚未分派部門';

/**
 * 加入成員：列出全部在職使用者，依主要所屬分組，勾選後一次加入。管理員多半要看到名單才認得出人，
 * 不必記得姓名或帳號；還沒有主要所屬的人排在最前面（通常就是這次要分派的人）。
 * 已是這個單位成員的人不列出。加入後的主要所屬與主管仍在成員列勾選，不在這裡決定。
 */
@Component({
  selector: 'nx-member-dialog',
  imports: [CompactDialog, DialogHeader, Notice, PendingButton, PersonBrowser],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #memberDialog
    class="platform-dialog"
    size="lg"
    [busy]="saving()"
    (cancel)="cancel($event)"
  >
    @if (unit(); as u) {
      <nx-dialog-header heading="加入成員" closeLabel="關閉加入成員" [detail]="u.name" />
      <div class="dialog-body">
        @if (error()) {
          <nx-notice tone="danger" [message]="error()">
            @if (!candidates()) {
              <button notice-actions type="button" (click)="load()">重新載入</button>
            }
          </nx-notice>
        }
        <nx-person-browser
          label="可加入的人員"
          emptyText="所有在職的使用者都已是這個單位的成員。"
          [people]="people()"
          [selected]="selected()"
          [loading]="!candidates() && !error()"
          [disabled]="saving()"
          (selectedChange)="selected.set($event)"
        />
      </div>
      <footer class="dialog-foot">
        <span class="dialog-note" role="status">加入後再於成員列勾選主要所屬或主管。</span>
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="saving() || null"
          (click)="close()"
        >
          取消
        </button>
        <button
          nxPending
          type="button"
          class="primary-button"
          [pending]="saving()"
          [disabled]="!saving() && !selected().length"
          (click)="save()"
        >
          {{ saving() ? '加入中…' : '加入 ' + selected().length + ' 人' }}
        </button>
      </footer>
    }
  </dialog>`,
})
export class MemberDialog {
  private readonly api = inject(OrganizationApi);
  private readonly store = inject(OrganizationStore);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('memberDialog');
  readonly unit = signal<UnitDto | null>(null);
  readonly candidates = signal<UnitCandidateDto[] | null>(null);
  readonly selected = signal<string[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');
  /** 依主要所屬分組：尚未分派的在前，其餘依單位樹的順序；同組依帳號（伺服器已排好）。 */
  readonly people = computed<BrowsePerson[]>(() => {
    const members = new Set((this.store.members() ?? []).map((member) => member.userId));
    const rows = unitRows(this.store.units());
    const order = new Map(rows.map((row, index) => [row.unit.id, index]));
    const paths = new Map(rows.map((row) => [row.unit.id, row.path.join(' › ')]));
    const rank = (id: string | null | undefined) => (id ? (order.get(id) ?? rows.length) : -1);
    return (this.candidates() ?? [])
      .filter((candidate) => !members.has(candidate.id))
      .sort((a, b) => rank(a.primaryUnitId) - rank(b.primaryUnitId))
      .map((candidate) => ({
        id: candidate.id,
        name: candidate.displayName,
        detail: candidate.account,
        group: (candidate.primaryUnitId && paths.get(candidate.primaryUnitId)) || UNASSIGNED,
      }));
  });

  open(unit: UnitDto) {
    this.unit.set(unit);
    this.selected.set([]);
    this.dialog().nativeElement.showModal();
    void this.load();
  }
  /** 每次開啟都重讀：名單與主要所屬可能在別的單位剛改過。 */
  async load() {
    const alive = this.scope.guard();
    this.candidates.set(null);
    this.error.set('');
    try {
      const candidates = await this.api.candidates();
      if (alive()) this.candidates.set(candidates);
    } catch (error) {
      if (alive()) this.error.set(safeMessage(error));
    }
  }
  close() {
    if (!this.saving()) this.dialog().nativeElement.close();
  }
  cancel(event: Event) {
    event.preventDefault();
    this.close();
  }

  async save() {
    const unit = this.unit();
    const ids = this.selected();
    if (!unit || !ids.length || this.saving()) return;
    const alive = this.scope.guard();
    this.saving.set(true);
    this.error.set('');
    let added = 0;
    try {
      // 逐一加入：每人一筆稽核（快照是此人的全部所屬），與修改成員相同；中途失敗就停，已加入的保留。
      for (const id of ids) {
        await this.api.saveMember(unit.id, id, { isPrimary: false, isManager: false });
        added++;
      }
      if (!alive()) return;
      this.dialog().nativeElement.close();
      this.store.toast.success(`已將 ${added} 人加入「${unit.name}」`);
    } catch (error) {
      if (!alive()) return;
      this.selected.set(ids.slice(added));
      const message = safeMessage(error);
      this.error.set(added ? `已加入 ${added} 人，其餘未加入：${message}` : message);
    } finally {
      if (alive()) {
        this.saving.set(false);
        if (added) this.store.membersChanged();
      }
    }
  }
}
