import { uniqueId } from '../browser/unique-id';
import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { Icon } from './icon';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';

/**
 * 原生最上層中的非模態資訊面板，供精簡指標與「規則／計算方式」說明共用。觸發器是 quiet-button
 * （圖示加標籤），hover、按下與開啟狀態沿用全站按鈕規則；若要自訂圖形（例如脈絡環），請在
 * `[popover-trigger]` 中放入專案內容。位於最上層，面板不會被捲動的父層裁切。
 * 面板內容用 `popovers.scss` 的 `.info-section`、`.info-table`、`.info-terms`（短條列用 `.form-note-list`）。
 * 有 `anchor` 時沒有觸發器：由其他控制項（例如溢出選單項目）呼叫 `toggle()`。
 */
@Component({
  selector: 'nx-info-popover',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (!anchor()) {
      <button
        #trigger
        type="button"
        class="quiet-button info-popover-trigger"
        [attr.aria-label]="description() || label()"
        [attr.title]="hint() || null"
        aria-haspopup="dialog"
        [attr.aria-expanded]="opened()"
        [attr.aria-controls]="id"
        (click)="toggle()"
      >
        <ng-content select="[popover-trigger]"
          ><nx-icon [name]="icon()" /><span>{{ label() }}</span></ng-content
        >
      </button>
    }
    <div
      #panel
      class="popover-panel info-popover-panel ui-density-compact"
      [id]="id"
      role="dialog"
      popover="auto"
      tabindex="-1"
      [attr.aria-label]="heading() ? null : panelLabel() || description() || label()"
      [attr.aria-labelledby]="heading() ? id + '-heading' : null"
      (toggle)="popover.sync($event)"
      (keydown.escape)="close($event)"
    >
      @if (heading()) {
        <h2 class="info-popover-heading" [id]="id + '-heading'">{{ heading() }}</h2>
      }
      <ng-content />
    </div>`,
  styles: `
    :host {
      display: inline-block;
      min-width: 0;
    }
    /* 外框、hover 與開啟狀態來自 .quiet-button；只留觸控面積內的留白可由呼叫端調整。 */
    .info-popover-trigger {
      padding: var(--info-popover-trigger-padding, var(--p-space-1) var(--p-space-2));
      white-space: nowrap;
    }
    .info-popover-panel {
      --info-popover-padding: var(--p-space-4);
      padding: var(--info-popover-padding);
      color: var(--secondary);
      overflow: auto;
      text-align: left;
      font-size: var(--text-caption);
      line-height: 1.6;
      outline: none;
    }
    /* 長面板捲動時標題固定在頂端，看得出正在讀哪一份說明。 */
    .info-popover-heading {
      position: sticky;
      top: calc(-1 * var(--info-popover-padding));
      z-index: 1;
      margin: calc(-1 * var(--info-popover-padding)) calc(-1 * var(--info-popover-padding))
        var(--p-space-3);
      padding: var(--p-space-3) var(--info-popover-padding);
      border-bottom: 1px solid var(--line);
      background: var(--surface);
      color: var(--ink);
      font-size: var(--text-label);
      font-weight: 600;
      line-height: var(--line-ui);
    }
  `,
})
export class InfoPopover {
  readonly label = input.required<string>();
  readonly description = input('');
  readonly icon = input('info');
  readonly align = input<'left' | 'right'>('left');
  /** 面板的名稱，與觸發器不同時使用（觸發器可能帶有即時值）。 */
  readonly panelLabel = input('');
  /** 面板頂端看得見的標題（捲動時固定），同時是面板名稱；短說明不需要。 */
  readonly heading = input('');
  readonly hint = input('');
  readonly width = input(320);
  /** 面板的最大高度（px），超過時面板內捲動；仍受視窗高度限制。 */
  readonly maxHeight = input(420);
  /** 把面板對齊到此元素，而不是渲染觸發器。 */
  readonly anchor = input<HTMLElement | null>(null);
  readonly id = uniqueId('nx-info');
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  /** 焦點回去的地方：觸發器，或開啟已錨定面板的元素。 */
  private opener: HTMLElement | null = null;
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) =>
      positionPopover(
        this.anchor() ?? this.trigger()!.nativeElement,
        panel,
        this.width(),
        this.align(),
        this.maxHeight(),
      ),
  });
  readonly opened = this.popover.opened;
  toggle() {
    if (this.opened()) this.close();
    else {
      this.opener = this.trigger()?.nativeElement ?? (document.activeElement as HTMLElement | null);
      this.popover.show();
      this.panel().nativeElement.focus();
    }
  }
  /** Escape 只關閉此面板：取消該按鍵，外層對話框才會保持開啟。 */
  close(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.popover.hide();
    this.opener?.focus();
  }
}
