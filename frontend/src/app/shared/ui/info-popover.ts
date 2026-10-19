import { uniqueId } from '../browser/unique-id';
import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { Icon } from './icon';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';

/**
 * 原生最上層中的非模態資訊面板，供精簡指標共用。觸發器預設是圖示加標籤；
 * 若要自訂圖形（例如脈絡環），請在 `[popover-trigger]` 中放入專案內容。位於最上層，
 * 面板不會被捲動的父層裁切。
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
        class="info-popover-trigger"
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
      [attr.aria-label]="panelLabel() || description() || label()"
      (toggle)="popover.sync($event)"
      (keydown.escape)="close($event)"
    >
      <ng-content />
    </div>`,
  styles: `
    :host {
      display: inline-block;
      min-width: 0;
    }
    .info-popover-trigger {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      min-height: var(--button-target);
      padding: var(--info-popover-trigger-padding, 0);
      white-space: nowrap;
      font-size: var(--text-caption);
      color: var(--secondary);
      border-radius: var(--p-radius-md);
    }
    .info-popover-trigger nx-icon {
      --icon-size: var(--icon-md);
    }
    .info-popover-trigger:hover {
      color: var(--signal);
    }
    .info-popover-trigger[aria-expanded='true'] {
      background: var(--hover);
      color: var(--ink);
    }
    .info-popover-panel {
      padding: var(--section-padding);
      color: var(--secondary);
      overflow: auto;
      text-align: left;
      font-size: var(--text-caption);
      outline: none;
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
  readonly hint = input('');
  readonly width = input(320);
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
        420,
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
