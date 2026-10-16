import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { Icon } from './icon';
import type { QueryChip, QueryState } from './query-state';

let sequence = 0;

/**
 * 資料頁的查詢列，搭配 `QueryState`：欄位只改草稿，按「查詢」（或在欄位按 Enter）才套用，「重設」套用預設值。
 * 草稿未套用時顯示提示；已套用條件顯示成 chip，可逐一移除。
 *
 * 投影位置：`[query-lead]` 放在欄位上方（會立即套用的分類 `ViewSwitch`）；預設 slot 放主要欄位；
 * `[query-more]` 放較少用的欄位，收在「＋條件」popover（需 `more`）。
 * 表單加 `novalidate`：必填欄位可能藏在關閉的 popover 裡，瀏覽器原生驗證會卡住送出，改由 state 的 `validate` 判斷。
 * 查詢中的按鈕用 `aria-disabled` 而非 `disabled`：停用的按鈕無法聚焦，剛按下的按鈕或移除 chip 後的焦點目標會讓焦點掉回 body。
 */
@Component({
  selector: 'nx-query-bar',
  imports: [Icon],
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
        <button type="submit" class="primary-button" [attr.aria-disabled]="busy() || null">
          <nx-icon [name]="busy() ? 'loading' : 'search'" />查詢
        </button>
        <button
          type="button"
          class="quiet-button"
          [attr.aria-disabled]="busy() || null"
          (click)="reset()"
        >
          重設
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
      (focusout)="leaveMore($event)"
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
    <div class="ui-query-status" [hidden]="!state().dirty() && !state().chips().length">
      <span class="ui-query-pending" role="status">
        @if (state().dirty()) {
          <nx-icon name="info" />條件已變更，按「查詢」套用
        }
      </span>
      @if (state().chips().length) {
        <ul class="ui-query-chips" aria-label="已套用條件">
          @for (chip of state().chips(); track chip.id; let i = $index) {
            <li>
              <span [title]="chip.label">{{ chip.label }}</span>
              <button
                type="button"
                data-chip-remove
                [attr.aria-label]="'移除條件：' + chip.label"
                [title]="'移除條件'"
                [attr.aria-disabled]="busy() || null"
                (click)="remove(chip, i)"
              >
                <nx-icon name="close" />
              </button>
            </li>
          }
        </ul>
      }
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
  readonly id = 'nx-query-' + ++sequence;
  private readonly form = viewChild.required<ElementRef<HTMLFormElement>>('form');
  private readonly moreTrigger = viewChild<ElementRef<HTMLButtonElement>>('moreTrigger');
  private readonly morePanel = viewChild.required<ElementRef<HTMLElement>>('morePanel');
  protected readonly morePopover = anchoredPopover({
    panel: () => this.morePanel().nativeElement,
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
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的查詢型別不同
  remove(chip: QueryChip<any>, index: number) {
    if (this.busy()) return;
    this.state().commit(chip.clear);
    // 被移除的 chip 按鈕會消失，焦點移到補上同位置的 chip；沒有 chip 了就移到「查詢」，避免焦點掉回 body。
    requestAnimationFrame(() => {
      const form = this.form().nativeElement;
      const buttons = form.querySelectorAll<HTMLButtonElement>('[data-chip-remove]');
      (
        buttons[Math.min(index, buttons.length - 1)] ??
        form.querySelector<HTMLButtonElement>('button[type="submit"]')
      )?.focus();
    });
  }
  toggleMore() {
    if (this.moreOpen()) this.closeMore();
    else {
      this.morePopover.show(this.moreTrigger()?.nativeElement);
      requestAnimationFrame(() =>
        this.morePanel()
          .nativeElement.querySelector<HTMLElement>('input, button, [tabindex="0"]')
          ?.focus(),
      );
    }
  }
  /** Tab 離開 popover 時關閉；popover="auto" 只處理點擊外面與 Escape。 */
  leaveMore(event: FocusEvent) {
    const next = event.relatedTarget as Node | null;
    if (
      this.moreOpen() &&
      next &&
      !this.morePanel().nativeElement.contains(next) &&
      next !== this.moreTrigger()?.nativeElement
    )
      this.morePopover.hide();
  }
  closeMore(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.morePopover.hide();
    this.moreTrigger()?.nativeElement.focus({ preventScroll: true });
  }
}
