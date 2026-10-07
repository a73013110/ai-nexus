import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, NavigationStart } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReaderOverlay } from '../../shared/browser/reader-overlay';
import { DocumentViewer } from './document-viewer';

@Component({
  selector: 'nx-reader-dialog',
  imports: [CompactDialog, DocumentViewer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="reader-dialog"
    aria-label="檔案預覽"
    [class.reader-expanded]="expanded()"
    (cancel)="$event.preventDefault(); overlay.close()"
    (close)="overlay.close()"
  >
    @defer (when overlay.target()) {
      @if (overlay.target(); as target) {
        <nx-document-viewer
          [target]="target"
          [embedded]="true"
          [expanded]="expanded()"
          (closeRequested)="overlay.close()"
          (expandRequested)="expanded.update(toggle)"
        />
      }
    } @placeholder {
      <p class="reader-loading" role="status">正在開啟預覽…</p>
    }
  </dialog>`,
})
export class ReaderDialog {
  readonly overlay = inject(ReaderOverlay);
  readonly expanded = signal(false);
  readonly toggle = (value: boolean) => !value;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  constructor() {
    effect(() => {
      const target = this.overlay.target(),
        dialog = this.dialog().nativeElement;
      if (target && !dialog.open) {
        this.expanded.set(false);
        dialog.showModal();
      } else if (!target && dialog.open) dialog.close();
    });
    let generation = this.overlay.auth.generation();
    effect(() => {
      const next = this.overlay.auth.generation();
      if (next !== generation) {
        generation = next;
        this.overlay.close();
      }
    });
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event instanceof NavigationStart) this.overlay.close();
      });
  }
}
