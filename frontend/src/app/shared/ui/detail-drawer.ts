import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';

let sequence = 0;

/** A modeless desktop inspector, becoming a native modal on narrow screens. */
@Component({
  selector: 'nx-detail-drawer',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    #dialog
    [id]="id"
    class="ui-detail-drawer ui-density-compact"
    [class.is-wide]="wide()"
    [attr.aria-labelledby]="id + '-title'"
    [attr.aria-modal]="modal() ? 'true' : null"
    (cancel)="$event.preventDefault(); close()"
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
  readonly wide = signal(false);
  readonly modal = signal(false);
  readonly id = `nx-drawer-${++sequence}`;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly body = viewChild.required<ElementRef<HTMLElement>>('body');
  private readonly narrow = matchMedia('(max-width: 1023px)');
  private returnFocus: HTMLElement | null = null;
  private suppressedCloses = 0;
  private destroyed = false;
  constructor() {
    const resize = () => {
      const dialog = this.dialog().nativeElement;
      if (!dialog.open || this.modal() === this.narrow.matches) return;
      const focused = document.activeElement as HTMLElement | null;
      ++this.suppressedCloses;
      dialog.close();
      this.showNative();
      if (focused && dialog.contains(focused)) focused.focus({ preventScroll: true });
    };
    const escape = (event: KeyboardEvent) => {
      if (
        event.key !== 'Escape' ||
        event.defaultPrevented ||
        this.modal() ||
        !this.dialog().nativeElement.open
      )
        return;
      if (document.querySelector('dialog:modal, [popover]:popover-open')) return;
      event.preventDefault();
      this.close();
    };
    this.narrow.addEventListener('change', resize);
    document.addEventListener('keydown', escape);
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.narrow.removeEventListener('change', resize);
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
    this.modal.set(this.narrow.matches);
    this.narrow.matches
      ? this.dialog().nativeElement.showModal()
      : this.dialog().nativeElement.show();
  }
  resetScroll() {
    this.body().nativeElement.scrollTop = 0;
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
    this.wide.set(false);
    this.closed.emit();
    if (this.returnFocus?.isConnected) this.returnFocus.focus({ preventScroll: true });
    this.returnFocus = null;
  }
}
