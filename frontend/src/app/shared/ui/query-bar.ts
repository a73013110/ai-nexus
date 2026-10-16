import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { Icon } from './icon';
import type { QueryChip, QueryState } from './query-state';

let sequence = 0;

/**
 * The query form of a data page over a `QueryState`: the fields edit the draft, 「查詢」 applies
 * it (Enter in a field too), 「重設」 applies the defaults. A pending edit is announced until it is
 * applied, and the applied conditions show as chips that remove one condition each.
 *
 * Slots: `[query-lead]` above the fields (a category `ViewSwitch` that applies at once), the
 * fields themselves, and `[query-more]` for the less used fields in a 「＋條件」 popover (`more`).
 * The form does not run native validation: closed popovers hold required fields, and the state's
 * `validate` decides.
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
        <button type="submit" class="primary-button" [disabled]="busy()">
          <nx-icon [name]="busy() ? 'loading' : 'search'" />查詢
        </button>
        <button type="button" class="quiet-button" [disabled]="busy()" (click)="reset()">
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
      [attr.aria-label]="moreLabel()"
      (toggle)="morePopover.sync($event)"
      (keydown.escape)="closeMore($event)"
    >
      <h3>{{ moreLabel() }}</h3>
      <div class="ui-query-more-fields"><ng-content select="[query-more]" /></div>
      <div class="ui-query-more-foot">
        <button type="button" class="secondary-button" (click)="closeMore()">完成</button>
        <button type="submit" class="primary-button" [disabled]="busy()">查詢</button>
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
                [disabled]="busy()"
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
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- any query shape
  readonly state = input.required<QueryState<any>>();
  readonly label = input('查詢條件');
  readonly busy = input(false);
  /** Shows the 「＋條件」 popover with the `[query-more]` fields. */
  readonly more = input(false);
  readonly moreLabel = input('更多條件');
  /** Fields in the popover that are set, shown on its button. */
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
    this.state().reset();
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- any query shape
  remove(chip: QueryChip<any>, index: number) {
    this.state().remove(chip);
    // The removed chip's button goes away: focus the chip that takes its place, else 「查詢」.
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
  closeMore(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.morePopover.hide();
    this.moreTrigger()?.nativeElement.focus({ preventScroll: true });
  }
}
