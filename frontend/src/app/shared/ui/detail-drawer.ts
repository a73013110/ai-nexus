import { uniqueId } from '../browser/unique-id';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { below } from '../browser/breakpoints';
import { mediaQuery } from '../browser/media-query';
import { Icon } from './icon';
import { ProductTourButton } from './product-tour-button';
import type { ProductTourDefinition } from './product-tour';

/**
 * 桌面版的非模態檢視器；窄螢幕時變成原生模態。重讀時舊內容留著，由呼叫端在內容區加
 * `.is-refreshing` 與 `aria-busy`，不換回骨架。
 */
@Component({
  selector: 'nx-detail-drawer',
  imports: [Icon, ProductTourButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    #dialog
    [id]="id"
    class="ui-detail-drawer ui-density-compact"
    [class.is-wide]="wide()"
    [attr.aria-labelledby]="id + '-title'"
    [attr.aria-modal]="modal() ? 'true' : null"
    (cancel)="onCancel($event)"
    (close)="didClose()"
  >
    <header class="ui-drawer-header">
      <h2 [id]="id + '-title'">{{ title() }}</h2>
      <div class="ui-drawer-actions">
        <ng-content select="[drawer-actions]" />
        <button
          type="button"
          class="icon-button drawer-resize"
          [attr.aria-label]="wide() ? '縮小詳情面板' : '展開詳情面板'"
          [title]="wide() ? '縮小詳情面板' : '展開詳情面板'"
          [attr.aria-pressed]="wide()"
          (click)="wide.set(!wide())"
        >
          <nx-icon [name]="wide() ? 'minimize' : 'maximize'" />
        </button>
        @if (tour(); as definition) {
          <nx-product-tour-button [tour]="definition" [compact]="true" />
        }
        <button
          type="button"
          autofocus
          class="icon-button"
          [attr.aria-label]="'關閉' + title()"
          [title]="'關閉' + title()"
          (click)="close()"
        >
          <nx-icon name="close" />
        </button>
      </div>
    </header>
    <ng-content select="[drawer-summary]" />
    <ng-content select="[drawer-tabs]" />
    <div #body class="ui-drawer-body"><ng-content /></div>
  </dialog>`,
  styleUrl: './detail-drawer.scss',
})
export class DetailDrawer {
  readonly title = input.required<string>();
  readonly closed = output<void>();
  /** 抽屜內容的導覽；觸發鈕在關閉鈕之前。 */
  readonly tour = input<ProductTourDefinition | null>(null);
  readonly wide = signal(false);
  readonly modal = signal(false);
  readonly id = uniqueId('nx-drawer');
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly body = viewChild.required<ElementRef<HTMLElement>>('body');
  /** 較窄的視口使用模態抽屜；較寬的則保留頁面可在其旁邊操作。 */
  private readonly narrow = mediaQuery(below('tablet'));
  private returnFocus: HTMLElement | null = null;
  private suppressedCloses = 0;
  private destroyed = false;
  constructor() {
    // 開啟中的抽屜在視口跨過斷點時，會以另一種形式重新開啟。
    let narrow = this.narrow();
    effect(() => {
      if (this.narrow() === narrow) return;
      narrow = this.narrow();
      untracked(() => {
        const dialog = this.dialog().nativeElement;
        if (!dialog.open || this.modal() === narrow) return;
        const focused = document.activeElement as HTMLElement | null;
        ++this.suppressedCloses;
        dialog.close();
        this.showNative();
        if (focused && dialog.contains(focused)) focused.focus({ preventScroll: true });
      });
    });
    const escape = (event: KeyboardEvent) => {
      if (
        event.key !== 'Escape' ||
        event.defaultPrevented ||
        this.modal() ||
        !this.dialog().nativeElement.open
      )
        return;
      // 提示的層永遠是開啟的，且從不為頁面持有 Escape。
      if (document.querySelector('dialog:modal, [popover]:popover-open:not(.toaster)')) return;
      event.preventDefault();
      this.close();
    };
    document.addEventListener('keydown', escape);
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      document.removeEventListener('keydown', escape);
      if (this.dialog().nativeElement.open) this.dialog().nativeElement.close();
    });
  }
  open() {
    if (!this.dialog().nativeElement.open) {
      this.returnFocus =
        document.activeElement instanceof HTMLElement ? document.activeElement : null;
      this.showNative();
    }
    this.resetScroll();
  }
  private showNative() {
    // 宿主可能從 effect 開啟抽屜；視口不能成為它的依賴。
    const modal = untracked(this.narrow);
    this.modal.set(modal);
    modal ? this.dialog().nativeElement.showModal() : this.dialog().nativeElement.show();
  }
  resetScroll() {
    this.body().nativeElement.scrollTop = 0;
  }
  onCancel(event: Event) {
    event.preventDefault();
    this.close();
  }
  close() {
    this.dialog().nativeElement.close();
  }
  didClose() {
    if (this.suppressedCloses) {
      --this.suppressedCloses;
      return;
    }
    if (this.destroyed) return;
    if (this.returnFocus?.isConnected) this.returnFocus.focus({ preventScroll: true });
    this.returnFocus = null;
    // 宿主會在 `closed` 時清空內容，所以要等退場完成；這段期間重新開啟會取消它。
    const dialog = this.dialog().nativeElement;
    void Promise.allSettled(dialog.getAnimations().map((item) => item.finished)).then(() => {
      if (this.destroyed || dialog.open) return;
      this.wide.set(false);
      this.closed.emit();
    });
  }
}
