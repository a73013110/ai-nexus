import { uniqueId } from '../browser/unique-id';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  computed,
  contentChildren,
  effect,
  input,
  viewChild,
} from '@angular/core';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { ChipList } from './chip-list';
import { Icon } from './icon';
import type { QueryChip, QueryState } from './query-state';
import type { TableModel } from './table-model';

interface AppliedChip {
  id: string;
  label: string;
  apply: () => void;
}

/**
 * 查詢列欄位對應的條件鍵（`nxQueryField="unitId"`）：已套用條件的 chip 依這些欄位在範本中的順序排列，
 * 調整欄位順序時 chip 自動跟著，不必另外維護 `chips` 的順序。
 */
@Directive({ selector: '[nxQueryField]' })
export class QueryField {
  readonly key = input.required<string>({ alias: 'nxQueryField' });
}

/**
 * 資料頁的查詢列，搭配 `QueryState`：欄位只改草稿，按「查詢」（或在欄位按 Enter）才套用，「重設」
 * 套用預設值。
 * 草稿未套用時顯示提示；已套用條件顯示成 chip，可逐一移除。
 *
 * 投影位置：`[query-lead]` 放在欄位上方（會立即套用的分類 `ViewSwitch`）；預設 slot 放主要欄位；
 * `[query-more]` 放較少用的欄位，收在「＋條件」popover（需 `more`）。
 * 表單加 `novalidate`：必填欄位可能藏在關閉的 popover 裡，瀏覽器原生驗證會卡住送出，改由 state 的
 * `validate` 判斷。
 * 欄位標上 `nxQueryField` 時，chip 依欄位順序排列（以 chip 的 `clear` 對應欄位）；對不到欄位的 chip 排在後面、維持原順序。
 * 查詢中的按鈕用 `aria-disabled` 而非 `disabled`：停用的按鈕無法聚焦，剛按下的按鈕或移除 chip
 * 後的焦點目標會讓焦點掉回 body。
 */
@Component({
  selector: 'nx-query-bar',
  imports: [ChipList, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './query-bar.scss',
  host: { class: 'ui-query-bar' },
  template: `<form #form novalidate [attr.aria-label]="label()" (submit)="submit($event)">
    <ng-content select="[query-lead]" />
    <div class="ui-query-row">
      <ng-content />
      @if (more()) {
        <button
          #moreTrigger
          type="button"
          class="secondary-button ui-query-more"
          aria-haspopup="dialog"
          [attr.aria-expanded]="moreOpen()"
          [attr.aria-controls]="id + '-more'"
          (click)="toggleMore()"
        >
          <nx-icon name="plus" />條件
          @if (moreCount()) {
            <span class="ui-count-label">{{ moreCount() }}</span>
          }
        </button>
      }
      <div class="ui-query-actions">
        <button
          type="submit"
          class="primary-button"
          [attr.aria-disabled]="busy() || null"
          [attr.aria-busy]="busy() || null"
        >
          <nx-icon [name]="busy() ? 'loading' : 'search'" />查詢
        </button>
        <button
          type="button"
          class="quiet-button"
          [attr.aria-disabled]="busy() || null"
          (click)="reset()"
        >
          <nx-icon name="reset" />重設
        </button>
      </div>
    </div>
    <div
      #morePanel
      popover="auto"
      role="dialog"
      class="popover-panel ui-query-more-panel ui-density-compact"
      [id]="id + '-more'"
      aria-label="更多條件"
      (toggle)="morePopover.sync($event)"
      (keydown.escape)="closeMore($event)"
    >
      <h3>更多條件</h3>
      <div class="ui-query-more-fields"><ng-content select="[query-more]" /></div>
      <div class="ui-query-more-foot">
        <button type="button" class="secondary-button" (click)="closeMore()">完成</button>
        <button type="submit" class="primary-button" [attr.aria-disabled]="busy() || null">
          查詢
        </button>
      </div>
    </div>
    @if (state().error()) {
      <p class="ui-query-error" role="alert"><nx-icon name="info" />{{ state().error() }}</p>
    }
    <div class="ui-query-status" [hidden]="!state().dirty() && !chips().length">
      <span class="ui-query-pending" role="status">
        @if (state().dirty()) {
          <nx-icon name="info" />條件已變更，按「查詢」套用
        }
      </span>
      <!-- 以 hidden 收起而非移除：最後一個 chip 移除後清單仍要在，才能把焦點交給「查詢」。 -->
      <nx-chip-list
        class="ui-query-chips"
        [hidden]="!chips().length"
        [chips]="chips()"
        [busy]="busy()"
        (chipRemove)="remove($event)"
        (allRemoved)="focusSubmit()"
      />
    </div>
  </form>`,
})
export class QueryBar {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的查詢型別不同
  readonly state = input.required<QueryState<any>>();
  readonly label = input('查詢條件');
  readonly busy = input(false);
  /** 顯示「＋條件」按鈕與 `[query-more]` 欄位的 popover。 */
  readonly more = input(false);
  /** popover 中已填的欄位數，顯示在按鈕上。 */
  readonly moreCount = input(0);
  readonly id = uniqueId('nx-query');
  private readonly fields = contentChildren(QueryField, { descendants: true });
  /**
   * 同一頁表格的欄位篩選（`[table]`）：chip 接在查詢條件後面，使用者只看到一列已套用條件；
   * 表格就不再自己顯示。
   */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的列型別不同
  readonly table = input<TableModel<any> | null>(null);
  /** 已套用條件的 chip：查詢條件依 `nxQueryField` 欄位的順序，欄位篩選排在最後。 */
  readonly chips = computed<AppliedChip[]>(() => {
    const chips = this.state().chips();
    const keys = this.fields().map((field) => field.key());
    // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的查詢型別不同
    const rank = (chip: QueryChip<any>) =>
      Math.min(
        keys.length,
        ...Object.keys(chip.clear)
          .map((key) => keys.indexOf(key))
          .filter((index) => index >= 0),
      );
    const ordered = keys.length ? [...chips].sort((a, b) => rank(a) - rank(b)) : chips;
    return [
      ...ordered.map((chip) => ({
        id: 'query:' + chip.id,
        label: chip.label,
        apply: () => this.state().commit(chip.clear),
      })),
      ...(this.table()?.chips() ?? []).map((chip) => ({
        id: 'column:' + chip.id,
        label: chip.label,
        apply: chip.remove,
      })),
    ];
  });
  constructor() {
    effect((onCleanup) => {
      const table = this.table();
      if (!table) return;
      table.chipsClaimed.set(true);
      onCleanup(() => table.chipsClaimed.set(false));
    });
  }
  private readonly form = viewChild.required<ElementRef<HTMLFormElement>>('form');
  private readonly moreTrigger = viewChild<ElementRef<HTMLButtonElement>>('moreTrigger');
  private readonly morePanel = viewChild.required<ElementRef<HTMLElement>>('morePanel');
  protected readonly morePopover = anchoredPopover({
    panel: () => this.morePanel().nativeElement,
    trigger: () => this.moreTrigger()?.nativeElement,
    closeOnFocusOut: true,
    place: (panel) => {
      const trigger = this.moreTrigger()?.nativeElement;
      if (trigger) positionPopover(trigger, panel, 320, 'left', 480);
    },
  });
  readonly moreOpen = this.morePopover.opened;

  submit(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    if (this.moreOpen()) this.closeMore();
    this.state().apply();
  }
  reset() {
    if (!this.busy()) this.state().reset();
  }
  remove(chip: AppliedChip) {
    if (!this.busy()) chip.apply();
  }
  /** 最後一個 chip 移除後焦點移到「查詢」，避免掉回 body。 */
  focusSubmit() {
    this.form().nativeElement.querySelector<HTMLButtonElement>('button[type="submit"]')?.focus();
  }
  toggleMore() {
    if (this.moreOpen()) this.closeMore();
    else {
      this.morePopover.show();
      requestAnimationFrame(() =>
        this.morePanel()
          .nativeElement.querySelector<HTMLElement>('input, button, [tabindex="0"]')
          ?.focus(),
      );
    }
  }
  closeMore(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.morePopover.hide();
    this.moreTrigger()?.nativeElement.focus({ preventScroll: true });
  }
}
