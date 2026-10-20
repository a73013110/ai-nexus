import { CompactDialog } from '@shared/ui/compact-dialog';
import { EmptyState } from '@shared/ui/empty-state';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { Router, NavigationStart } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReaderOverlay } from '@shared/browser/reader-overlay';
import { DocumentViewer } from './document-viewer';

@Component({
  selector: 'nx-reader-dialog',
  imports: [CompactDialog, EmptyState, DocumentViewer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './reader-dialog.scss',
  template: `<dialog
    nxCompactDialog
    size="full"
    #dialog
    class="platform-dialog reader-dialog"
    aria-label="檔案預覽"
    (cancel)="onCancel($event)"
    (close)="overlay.close()"
  >
    @defer (when overlay.target()) {
      @if (overlay.target(); as target) {
        <nx-document-viewer
          [target]="target"
          [embedded]="true"
          (closeRequested)="overlay.close()"
        />
      }
    } @placeholder {
      <nx-empty-state><p>正在開啟預覽…</p></nx-empty-state>
    }
  </dialog>`,
})
export class ReaderDialog {
  readonly overlay = inject(ReaderOverlay);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  onCancel(event: Event) {
    event.preventDefault();
    this.overlay.close();
  }
  constructor() {
    effect(() => {
      const target = this.overlay.target(),
        dialog = this.dialog().nativeElement;
      if (target && !dialog.open) dialog.showModal();
      else if (!target && dialog.open) dialog.close();
    });
    // 換帳號就關閉預覽：上一個帳號開啟的文件不留在新帳號的畫面上。
    let account = this.overlay.auth.guard();
    effect(() => {
      if (account()) return;
      account = this.overlay.auth.guard();
      this.overlay.close();
    });
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event instanceof NavigationStart) this.overlay.close();
      });
  }
}
