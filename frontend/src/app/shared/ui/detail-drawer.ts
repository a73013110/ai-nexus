import { uniqueId } from '../browser/unique-id';
import {
  ChangeDetectionStrategy,
  Component,
  contentChild,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { below } from '../browser/breakpoints';
import { exitFinished } from '../browser/exit-motion';
import { mediaQuery } from '../browser/media-query';
import { bringToFront, ownsEscape } from '../browser/top-layer';
import { DrawerSummary } from './drawer-summary';
import { Icon } from './icon';
import { ProductTourButton } from './product-tour-button';
import type { ProductTourDefinition } from './product-tour';

export type DrawerSize = 'standard' | 'wide';

const STORAGE_PREFIX = 'ai-nexus.drawer.';

function storedSize(key: string): DrawerSize | null {
  try {
    const stored = localStorage.getItem(STORAGE_PREFIX + key);
    return stored === 'standard' || stored === 'wide' ? stored : null;
  } catch {
    return null;
  }
}

/**
 * 詳情抽屜：桌面版非模態，窄螢幕變原生模態。內容依序是 `drawer-actions`（上下筆、操作）、`nx-drawer-summary`、
 * `drawer-tabs`、內文與 `nx-drawer-foot`。重讀時舊內容留著，由呼叫端在內容區加 `.is-refreshing` 與 `aria-busy`。
 * 非模態時同時是 manual popover：和對話框、放大的表格一樣在 top layer，依開啟順序疊放；
 * 仍是開著的 `dialog[open]`，關閉、焦點還原與 `close` 事件照對話框的規則。
 */
@Component({
  selector: 'nx-detail-drawer',
  imports: [Icon, ProductTourButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    #dialog
    [id]="id"
    popover="manual"
    class="ui-detail-drawer ui-density-compact"
    [attr.data-size]="size()"
    [attr.aria-labelledby]="id + '-title'"
    [attr.aria-modal]="modal() ? 'true' : null"
    (cancel)="onCancel($event)"
    (close)="didClose()"
  >
    <header class="ui-drawer-header" [class.has-summary]="summary()">
      <div class="ui-drawer-bar">
        <h2 [id]="id + '-title'">{{ title() }}</h2>
        <div class="ui-drawer-actions">
          <ng-content select="[drawer-actions]" />
          <button
            type="button"
            class="icon-button drawer-resize"
            [attr.aria-label]="size() === 'wide' ? '縮小詳情面板' : '展開詳情面板'"
            [title]="size() === 'wide' ? '縮小詳情面板' : '展開詳情面板'"
            [attr.aria-pressed]="size() === 'wide'"
            (click)="toggleSize()"
          >
            <nx-icon [name]="size() === 'wide' ? 'minimize' : 'maximize'" />
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
      </div>
      <ng-content select="nx-drawer-summary" />
      <ng-content select="[drawer-tabs]" />
    </header>
    <div #body class="ui-drawer-body"><ng-content /></div>
    <ng-content select="nx-drawer-foot" />
  </dialog>`,
  styleUrl: './detail-drawer.scss',
})
export class DetailDrawer {
  /** 抽屜的無障礙名稱；有摘要時是摘要上方的小標，沒有摘要時就是標題。 */
  readonly title = input.required<string>();
  /** 記住這種抽屜的大小；必填，每種抽屜各自一把，換頁或重新整理都沿用。 */
  readonly preferenceKey = input.required<string>();
  /** 使用者還沒調整過時的大小；差異比對這類需要寬度的內容用 `wide`。 */
  readonly defaultSize = input<DrawerSize>('standard');
  readonly closed = output<void>();
  /** 抽屜內容的導覽；觸發鈕在關閉鈕之前。 */
  readonly tour = input<ProductTourDefinition | null>(null);
  readonly size = linkedSignal<DrawerSize>(
    () => storedSize(this.preferenceKey()) ?? this.defaultSize(),
  );
  readonly modal = signal(false);
  readonly id = uniqueId('nx-drawer');
  protected readonly summary = contentChild(DrawerSummary);
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
        this.close();
        this.showNative();
        if (focused && dialog.contains(focused)) focused.focus({ preventScroll: true });
      });
    });
    // 非模態沒有原生的 Esc 關閉：焦點在抽屜裡或頁面上都收（規則見 `ownsEscape`）。
    const escape = (event: KeyboardEvent) => {
      const dialog = this.dialog().nativeElement;
      if (event.key !== 'Escape' || this.modal() || !dialog.open || !ownsEscape(dialog, event))
        return;
      event.preventDefault();
      this.close();
    };
    document.addEventListener('keydown', escape);
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      document.removeEventListener('keydown', escape);
      if (this.dialog().nativeElement.open) this.close();
    });
  }
  /** 已開著時換內容；被之後開的圖層（例如放大的表格）蓋住就移回最上面。 */
  open() {
    const dialog = this.dialog().nativeElement;
    if (!dialog.open) {
      this.returnFocus =
        document.activeElement instanceof HTMLElement ? document.activeElement : null;
      this.showNative();
    } else bringToFront(dialog);
    this.resetScroll();
  }
  private showNative() {
    // 宿主可能從 effect 開啟抽屜；視口不能成為它的依賴。
    const modal = untracked(this.narrow);
    this.modal.set(modal);
    const dialog = this.dialog().nativeElement;
    if (modal) dialog.showModal();
    else {
      // 先 show 再進 top layer：開著的 popover 不能再 show。
      dialog.show();
      dialog.showPopover?.();
    }
  }
  resetScroll() {
    this.body().nativeElement.scrollTop = 0;
  }
  toggleSize() {
    const size = this.size() === 'wide' ? 'standard' : 'wide';
    this.size.set(size);
    try {
      localStorage.setItem(STORAGE_PREFIX + this.preferenceKey(), size);
    } catch {
      /* 選用偏好。 */
    }
  }
  onCancel(event: Event) {
    event.preventDefault();
    this.close();
  }
  close() {
    const dialog = this.dialog().nativeElement;
    if (dialog.matches(':popover-open')) dialog.hidePopover();
    dialog.close();
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
    void exitFinished(dialog).then(() => {
      if (this.destroyed || dialog.open) return;
      this.closed.emit();
    });
  }
}
