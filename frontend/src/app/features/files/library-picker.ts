import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import type { LibraryFile } from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { FileBrowser } from './file-browser';
import { FileLibraryStore } from './file-library-store';
import { Icon } from '../../shared/ui/icon';
import { SearchField } from '../../shared/ui/search-field';

@Component({
  selector: 'nx-library-picker',
  imports: [FileBrowser, Icon, SearchField],
  providers: [ViewScope, FileLibraryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
      type="button"
      [class]="compact() ? 'icon-button' : 'secondary-button'"
      [disabled]="disabled()"
      [attr.aria-label]="label()"
      [title]="label()"
      (click)="open()"
    >
      <nx-icon name="files" />
      @if (!compact()) {
        {{ label() }}
      }
    </button>
    <dialog #dialog class="platform-dialog file-picker-dialog" aria-label="從檔案庫選取">
      <div class="dialog-scroll">
        <div class="dialog-heading">
          <div>
            <span class="panel-eyebrow">重用已保存的檔案</span>
            <h2>從檔案庫選取</h2>
          </div>
          <button class="icon-button" aria-label="關閉檔案選取" (click)="dialog.close()">
            <nx-icon name="close" />
          </button>
        </div>
        <nx-search-field
          label="搜尋檔案庫"
          placeholder="搜尋檔案名稱"
          [value]="store.search()"
          (valueChange)="store.find($event)"
        />
        @if (store.error()) {
          <p class="error-banner" role="alert">{{ store.error() }}</p>
        }
        @if (store.loading()) {
          <p role="status" class="form-note">正在載入檔案…</p>
        } @else if (!store.items().length) {
          <p class="empty-state">
            沒有符合的檔案。已送出的對話附件與上傳至知識庫的原檔都會保存在這裡。
          </p>
        } @else {
          <nx-file-browser
            [items]="store.items()"
            layout="list"
            [selectable]="true"
            (chosen)="pick($event)"
          />
        }
        <div class="file-pagination">
          <span>{{ store.total() }} 份檔案</span
          ><button
            class="quiet-button"
            [disabled]="store.loading() || !store.offset()"
            (click)="store.page(-1)"
          >
            上一頁</button
          ><button
            class="quiet-button"
            [disabled]="store.loading() || !store.canNext()"
            (click)="store.page(1)"
          >
            下一頁
          </button>
        </div>
      </div>
    </dialog>`,
})
export class LibraryPicker {
  readonly store = inject(FileLibraryStore);
  readonly disabled = input(false);
  readonly compact = input(false);
  readonly label = input('從檔案庫加入');
  readonly chosen = output<LibraryFile>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  open() {
    this.dialog().nativeElement.showModal();
    void this.store.load();
  }
  pick(file: LibraryFile) {
    this.dialog().nativeElement.close();
    this.chosen.emit(file);
  }
}
