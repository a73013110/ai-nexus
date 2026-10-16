import { Notice } from '@shared/ui/notice';
import { DialogHeader } from '@shared/ui/dialog-header';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { EmptyState } from '@shared/ui/empty-state';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import type { LibraryFileDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { apiHref } from '@core/api/api-client';
import { ReaderLink } from '@shared/browser/reader-link';
import { formatBytes, formatDate } from '@shared/browser/format';
import { FileLibraryStore } from '../file-library-store';
import { Icon } from '@shared/ui/icon';
import { SearchField } from '@shared/ui/search-field';
import { TablePagination } from '@shared/ui/data-table';

@Component({
  selector: 'nx-library-picker',
  imports: [
    Notice,
    CompactDialog,
    DialogHeader,
    ReaderLink,
    Icon,
    SearchField,
    EmptyState,
    TablePagination,
  ],
  providers: [ViewScope, FileLibraryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './library-picker.scss',
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
    <dialog nxCompactDialog size="lg" #dialog class="platform-dialog" aria-label="從檔案庫選取">
      <nx-dialog-header heading="從檔案庫選取" closeLabel="關閉檔案選取">
        <nx-search-field
          appearance="bare"
          label="搜尋檔案庫"
          placeholder="搜尋檔案庫中的檔案"
          [value]="store.search()"
          (valueChange)="store.find($event)"
        />
      </nx-dialog-header>
      <div #body class="dialog-body dialog-body-flush" [attr.aria-busy]="store.loading()">
        @if (store.error()) {
          <nx-notice tone="danger" [message]="store.error()" />
        }
        @if (store.loading() && !store.items().length) {
          <p role="status" class="picker-note">正在載入檔案…</p>
        } @else if (!store.items().length) {
          <nx-empty-state
            ><p>
              沒有符合的檔案。已送出的對話附件與上傳至知識庫的原檔都會保存在這裡。
            </p></nx-empty-state
          >
        } @else {
          <ul class="picker-list">
            @for (item of store.items(); track item.file.id) {
              <li>
                <button
                  type="button"
                  class="picker-choose"
                  [attr.aria-label]="'選取 ' + item.file.fileName"
                  (click)="pick(item)"
                >
                  <span class="picker-thumb" aria-hidden="true">
                    @if (item.file.isImage) {
                      <img [src]="url(item.file.id)" alt="" loading="lazy" decoding="async" />
                    } @else {
                      {{ extension(item.file.fileName) }}
                    }
                  </span>
                  <span class="picker-copy"
                    ><strong [title]="item.file.fileName">{{ item.file.fileName }}</strong
                    ><small
                      >{{ bytes(item.file.size) }} · {{ date(item.createdAt) }}
                      @if (item.usages[0]; as usage) {
                        · {{ usage.name }}
                      }
                    </small></span
                  >
                  <nx-icon name="plus" class="picker-add" />
                </button>
                <a
                  class="icon-button"
                  [nxReaderLink]="item.file.id"
                  [readerAttachment]="true"
                  [attr.aria-label]="'預覽 ' + item.file.fileName"
                  title="預覽"
                  ><nx-icon name="eye"
                /></a>
              </li>
            }
          </ul>
        }
      </div>
      @if (store.paged()) {
        <footer class="dialog-foot">
          <nx-table-pagination
            mode="offset"
            [pageIndex]="store.pageIndex()"
            [pageSize]="store.pageSize"
            [pageSizes]="[store.pageSize]"
            [count]="store.items().length"
            [total]="store.total()"
            [busy]="store.loading()"
            (pageChange)="turn(store.pageIndex() + $event)"
            (pageSelect)="turn($event)"
          />
        </footer>
      }
    </dialog>`,
})
export class LibraryPicker {
  readonly store = inject(FileLibraryStore);
  readonly disabled = input(false);
  readonly compact = input(false);
  readonly label = input('從檔案庫加入');
  readonly chosen = output<LibraryFileDto>();
  readonly bytes = formatBytes;
  readonly date = (value: string) => formatDate(value).split(' ')[0];
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly body = viewChild.required<ElementRef<HTMLElement>>('body');
  open() {
    this.dialog().nativeElement.showModal();
    void this.store.load();
  }
  /** 翻頁回到清單開頭。 */
  turn(index: number) {
    this.store.select(index);
    this.body().nativeElement.scrollTop = 0;
  }
  pick(file: LibraryFileDto) {
    this.dialog().nativeElement.close();
    this.chosen.emit(file);
  }
  url(id: string) {
    return apiHref('/api/v1/attachments/{id}/content', { path: { id } });
  }
  extension(name: string) {
    return name.split('.').pop()?.toUpperCase().slice(0, 4) || 'FILE';
  }
}
