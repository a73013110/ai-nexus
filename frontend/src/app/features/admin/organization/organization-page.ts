import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { DirectoryUserDto, UnitMemberDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ViewScope } from '@shared/browser/view-scope';
import { Card } from '@shared/ui/card';
import { Checkbox } from '@shared/ui/checkbox';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { MenuPanel, type MenuAction } from '@shared/ui/menu-panel';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import type { UnitImportResultDto } from '@core/api/schema';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { UnitImportDialog } from './imports/unit-import-dialog';
import { resultSummary } from './imports/unit-import-presentation';
import { OrganizationStore } from './organization-store';
import { organizationTour } from './organization-tour';
import { UnitDialog } from './unit-dialog';
import { MAX_DEPTH, moved, type UnitRow } from './unit-tree';

/**
 * 組織：部門樹與每個單位的直屬成員。單位只用來計算資料範圍與主管關係，不授予功能（ADR 0017）；
 * 部門可手建，也可從公文系統匯入（`organization.import`）；匯入的單位名稱、上層、啟用、順序與主管以公文系統為準。
 */
@Component({
  selector: 'nx-organization-page',
  imports: [
    Card,
    Checkbox,
    ConfirmDialog,
    EmptyState,
    FeaturePage,
    Icon,
    MenuPanel,
    Notice,
    PendingButton,
    SearchField,
    StatusBadge,
    UnitDialog,
    UnitImportDialog,
  ],
  providers: [OrganizationStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './organization-page.html',
  styleUrl: './organization-page.scss',
})
export class OrganizationPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(OrganizationStore);
  readonly maxDepth = MAX_DEPTH;
  readonly canImport = computed(() => this.session.has('organization.import'));
  readonly tour = computed(() =>
    organizationTour({ testing: this.session.testing(), canImport: this.canImport() }),
  );
  private readonly menu = viewChild.required(MenuPanel);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly dialog = viewChild.required(UnitDialog);
  private readonly importDialog = viewChild.required(UnitImportDialog);
  readonly people = directorySearch(() => (this.store.members() ?? []).map((m) => m.userId), {
    includeSelf: true,
  });
  /** 骨架列的寬度（%），長短交錯才不像一整塊色塊。 */
  protected readonly skeleton = [72, 56, 64, 48, 60];
  /** 開啟操作選單的那一列。 */
  private readonly menuRow = signal<UnitRow | null>(null);
  readonly actions = computed<MenuAction[]>(() => {
    const row = this.menuRow();
    if (!row) return [];
    const units = this.store.units();
    const locked = this.store.locked();
    return [
      { id: 'edit', label: '編輯', icon: 'edit', disabled: locked },
      {
        id: 'child',
        label: '新增下層單位',
        icon: 'plus',
        disabled: locked || row.depth >= MAX_DEPTH,
      },
      {
        id: 'up',
        label: '上移',
        icon: 'arrow',
        disabled: locked || !moved(units, row.unit.id, -1),
      },
      {
        id: 'down',
        label: '下移',
        icon: 'down',
        disabled: locked || !moved(units, row.unit.id, 1),
      },
      { id: 'delete', label: '刪除', icon: 'trash', danger: true, disabled: locked },
    ];
  });

  isMenuOpen(trigger: HTMLElement) {
    return this.menu().isOpenFor(trigger);
  }
  openMenu(row: UnitRow, trigger: HTMLElement) {
    if (this.menu().isOpenFor(trigger)) return this.menu().close();
    this.menuRow.set(row);
    this.menu().open(trigger);
  }
  act(action: string) {
    const row = this.menuRow();
    if (!row) return;
    switch (action) {
      case 'edit':
        return this.dialog().edit(row.unit);
      case 'child':
        return this.dialog().create(row.unit);
      case 'up':
        return void this.store.move(row.unit, -1);
      case 'down':
        return void this.store.move(row.unit, 1);
      case 'delete':
        return void this.remove(row);
    }
  }
  openImport() {
    this.importDialog().open();
  }
  /** 匯入已套用：單位、成員與主管都可能變了，全部重讀。 */
  imported(result: UnitImportResultDto) {
    this.store.toast.success(resultSummary(result));
    this.store.load();
  }
  create(parent = false) {
    this.dialog().create(parent ? this.store.selected() : null);
  }
  editSelected() {
    const unit = this.store.selected();
    if (unit) this.dialog().edit(unit);
  }

  private async remove(row: UnitRow) {
    if (
      await this.confirmation().ask({
        title: `刪除「${row.unit.name}」？`,
        message: '刪除後無法復原；仍有下層單位或成員時無法刪除。不再使用的單位建議改為停用。',
        confirm: '刪除',
        danger: true,
      })
    )
      await this.store.remove(row.unit);
  }

  async add(user: DirectoryUserDto) {
    // 還沒有主要所屬的人通常是第一次分派部門，由管理員自行勾選，不自動設定。
    if (await this.store.saveMember(user.id, false, false, true)) this.people.clear();
  }
  setPrimary(member: UnitMemberDto, value: boolean) {
    void this.store.saveMember(
      member.userId,
      value,
      member.isManager,
      false,
      `primary:${member.userId}`,
    );
  }
  setManager(member: UnitMemberDto, value: boolean) {
    void this.store.saveMember(
      member.userId,
      member.isPrimary,
      value,
      false,
      `manager:${member.userId}`,
    );
  }
  async removeMember(member: UnitMemberDto) {
    const unit = this.store.selected();
    if (
      unit &&
      (await this.confirmation().ask({
        title: `將 ${member.displayName} 移出「${unit.name}」？`,
        message: member.isPrimary
          ? '這是此人的主要所屬單位，移出後不會自動指定其他單位。'
          : '移出後此人不再屬於這個單位，其他單位的所屬不受影響。',
        confirm: '移出',
        danger: true,
      }))
    )
      await this.store.removeMember(member);
  }
}
