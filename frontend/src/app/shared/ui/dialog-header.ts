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
 * 其他內容則放在關閉鈕之前。
 * `maximizable` adds the 放大視窗 toggle of `CompactDialog.maximized`; while maximized inside an
 * `nxIdleChrome` 的檢視者會讓標題列浮在內容之上，並隨其工具列淡出（idle-chrome.ts）。
 */
let headerSequence = 0;

@Component({
  selector: 'nx-dialog-header',
  imports: [Icon, ProductTourButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'dialog-bar', '[attr.data-chrome]': 'chrome()' },
  template: `<div class="dialog-title">
      <h2 [id]="titleId">{{ heading() }}</h2>
      <ng-content select="[dialog-title]" />
      @if (detail()) {
        <small>{{ detail() }}</small>
      }
    </div>
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
        class="icon-button"
        [attr.aria-label]="closeLabel()"
        title="關閉（Esc）"
        (click)="close()"
      >
        <nx-icon name="close" />
      </button>
    }`,
})
export class DialogHeader {
  readonly heading = input.required<string>();
  readonly detail = input('');
  readonly closable = input(true);
  readonly closeLabel = input('關閉');
  readonly maximizable = input(false, { transform: booleanAttribute });
  /** 對話框內容的導覽；觸發鈕在關閉鈕之前。 */
  readonly tour = input<ProductTourDefinition | null>(null);
  protected readonly dialog = inject(CompactDialog, { optional: true });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly maximized = computed(() => !!this.dialog?.maximized());
  protected readonly chrome = idleChromePart(this.host.nativeElement, this.maximized);
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
