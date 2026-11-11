import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  computed,
  inject,
  input,
} from '@angular/core';
import { requestDialogClose } from '../browser/dialog-close';
import { CompactDialog } from './compact-dialog';
import { Icon } from './icon';
import { idleChromePart } from './idle-chrome';
import { ProductTourButton } from './product-tour-button';
import type { ProductTourDefinition } from './product-tour';

/**
 * `.platform-dialog` 的標題列：標題、可選的細節與工具，以及關閉鈕。
 * 標記為 `dialog-title` 的內容（例如狀態徽章）會放在標題旁邊、h2 之外，讓對話框的無障礙名稱只剩標題；
 * 其他內容則放在關閉鈕之前，與視窗動作同在 `.dialog-actions`。
 * 開啟單筆紀錄（表單、單據）時可加 `icon`（標題左側的圖示方塊）與標記為 `dialog-subtitle` 的第二行（種類、單號、時間，各項一個子元素、分隔點由樣式畫），
 * 標題列改為兩行、標題放大。
 * `maximizable` 多一顆切換 `CompactDialog.maximized` 的「放大視窗」。
 * 在檢視器（`nxIdleChrome` 範圍）裡，標題列不佔版面：標題與動作各是一顆浮在內容上的膠囊，與工具列一起
 * 閒置淡出（idle-chrome.ts；樣式在 dialogs.scss 的 `[data-chrome]`），放大與否都是同一套版面。
 */
let headerSequence = 0;

@Component({
  selector: 'nx-dialog-header',
  imports: [Icon, ProductTourButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'dialog-bar',
    '[class.is-record]': '!!icon()',
    '[attr.data-chrome]': 'chrome()',
  },
  template: `@if (icon(); as name) {
      <span class="dialog-icon" aria-hidden="true"><nx-icon [name]="name" /></span>
    }
    <div class="dialog-heading">
      <div class="dialog-title">
        <h2 [id]="titleId" [attr.title]="chrome() ? heading() : null">{{ heading() }}</h2>
        <ng-content select="[dialog-title]" />
        @if (detail()) {
          <small>{{ detail() }}</small>
        }
      </div>
      <ng-content select="[dialog-subtitle]" />
    </div>
    <div class="dialog-actions">
      <ng-content />
      @if (maximizable() && dialog) {
        <button
          type="button"
          class="icon-button"
          [attr.aria-label]="maximized() ? '還原視窗' : '放大視窗'"
          [title]="maximized() ? '還原視窗' : '放大視窗'"
          (click)="dialog.maximized.set(!maximized())"
        >
          <nx-icon [name]="maximized() ? 'minimize' : 'maximize'" />
        </button>
      }
      @if (tour(); as definition) {
        <nx-product-tour-button [tour]="definition" [compact]="true" />
      }
      @if (closable()) {
        <button
          type="button"
          class="icon-button dialog-close"
          [attr.aria-label]="closeLabel()"
          title="關閉（Esc）"
          (click)="close()"
        >
          <nx-icon name="close" />
        </button>
      }
    </div>`,
})
export class DialogHeader {
  readonly heading = input.required<string>();
  readonly detail = input('');
  readonly closable = input(true);
  readonly closeLabel = input('關閉');
  /** 單筆紀錄的圖示（`nx-icon` 名稱），有值時標題列改為紀錄樣式。 */
  readonly icon = input<string | null>(null);
  readonly maximizable = input(false, { transform: booleanAttribute });
  /** 對話框內容的導覽；觸發鈕在關閉鈕之前。 */
  readonly tour = input<ProductTourDefinition | null>(null);
  protected readonly dialog = inject(CompactDialog, { optional: true });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly maximized = computed(() => !!this.dialog?.maximized());
  /** 有值代表在檢視器裡：標題列是浮在內容上的膠囊，跟著範圍出現與淡出。 */
  protected readonly chrome = idleChromePart(this.host.nativeElement);
  protected readonly titleId = `nx-dialog-header-${++headerSequence}`;
  constructor() {
    // showModal() 之後渲染的內容在 beforetoggle 時還不存在，所以在這裡為對話框命名。
    afterNextRender(() => {
      const dialog = this.host.nativeElement.closest('dialog');
      if (dialog && !dialog.hasAttribute('aria-label'))
        dialog.setAttribute('aria-labelledby', this.titleId);
    });
  }
  close() {
    const dialog = this.host.nativeElement.closest('dialog');
    if (dialog) requestDialogClose(dialog);
  }
}
