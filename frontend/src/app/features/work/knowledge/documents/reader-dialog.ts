import { CompactDialog } from '@shared/ui/compact-dialog';
import { Icon } from '@shared/ui/icon';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  linkedSignal,
  viewChild,
} from '@angular/core';
import { Router, NavigationStart } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { exitFinished } from '@shared/browser/exit-motion';
import { ReaderOverlay, type ReaderTarget } from '@shared/browser/reader-overlay';
import { DocumentViewer } from './document-viewer';

@Component({
  selector: 'nx-reader-dialog',
  imports: [CompactDialog, Icon, DocumentViewer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './reader-dialog.scss',
  template: `<dialog
    nxCompactDialog
    size="full"
    #dialog
    class="platform-dialog reader-dialog"
    aria-label="檔案預覽"
    (cancel)="onCancel($event)"
    (close)="closed()"
  >
    @defer (when shown()) {
      @if (shown(); as target) {
        <nx-document-viewer
          [target]="target"
          [embedded]="true"
          (closeRequested)="overlay.close()"
        />
      }
    } @placeholder {
      <p class="reader-placeholder" role="status"><nx-icon name="loading" />正在開啟預覽…</p>
    }
  </dialog>`,
})
export class ReaderDialog {
  readonly overlay = inject(ReaderOverlay);
  /**
   * 畫面上的檔案：跟著要開啟的檔案走，關閉後留到退場結束才清掉，
   * 對話框帶著原本的內容縮小淡出，而不是先變成空白再退場。
   */
  protected readonly shown = linkedSignal<ReaderTarget | null, ReaderTarget | null>({
    source: this.overlay.target,
    computation: (target, previous) => target ?? previous?.value ?? null,
  });
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  onCancel(event: Event) {
    event.preventDefault();
    this.overlay.close();
  }
  protected closed() {
    this.overlay.close();
    const dialog = this.dialog().nativeElement;
    // 退場中重新開啟（dialog 又是 open）就保留新內容。
    void exitFinished(dialog).then(() => dialog.open || this.shown.set(null));
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
