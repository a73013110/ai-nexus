import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { ReaderTarget } from '@shared/browser/reader-overlay';
import { ReaderLink } from '@shared/browser/reader-link';
import { Icon } from './icon';

/**
 * 清單上的一個檔案：`reader` 決定點開時用哪個閱讀器（附件、容器內的檔案、遠端檔案），`thumbnail` 有值時顯示縮圖，
 * `detail` 是檔名下的一行說明（大小、分析方式、上傳者…），由呼叫端依情境組好。
 */
export interface FileListItem {
  key: string;
  fileName: string;
  detail: string;
  reader: Omit<ReaderTarget, 'page'>;
  downloadUrl: string;
  thumbnail?: string | null;
}

/**
 * 檔案卡片清單：點卡片在閱讀器預覽，右側下載原檔，`editable` 時多一個移除鈕。
 * 草稿與訊息的附件、公文系統表單的附件共用；資料來源不同，只差在呼叫端怎麼組 {@link FileListItem}。
 */
@Component({
  selector: 'nx-file-list',
  imports: [Icon, ReaderLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.is-stacked]': 'stacked()' },
  styleUrl: './file-list.scss',
  template: `<ul class="attachment-list" [attr.aria-label]="label()">
    @for (file of files(); track file.key) {
      <li class="attachment-card" animate.enter="attachment-enter">
        <a
          class="attachment-preview"
          [nxReaderLink]="file.reader.id"
          [readerAttachment]="file.reader.attachment"
          [readerScope]="file.reader.scope ?? null"
          [readerRemote]="file.reader.remote ?? null"
          [attr.aria-label]="'閱讀' + label() + '：' + file.fileName"
          [title]="file.fileName"
        >
          @if (file.thumbnail) {
            <img
              [src]="file.thumbnail"
              [alt]="file.fileName"
              width="48"
              height="48"
              loading="lazy"
            />
          } @else {
            <span class="attachment-symbol"><nx-icon name="document" /></span>
          }
          <span class="attachment-copy"
            ><strong>{{ file.fileName }}</strong
            ><small>{{ file.detail }}</small></span
          >
          <nx-icon class="attachment-open" name="chevron" />
        </a>
        <a
          class="icon-button attachment-download"
          [href]="file.downloadUrl"
          [attr.aria-label]="'下載' + label() + '：' + file.fileName"
          title="下載原檔"
          ><nx-icon name="download"
        /></a>
        @if (editable()) {
          <button
            type="button"
            class="icon-button attachment-remove"
            [attr.aria-label]="'移除' + label() + '：' + file.fileName"
            (click)="remove.emit(file.key)"
          >
            <nx-icon name="close" />
          </button>
        }
      </li>
    }
  </ul>`,
})
export class FileList {
  readonly files = input.required<FileListItem[]>();
  /** 清單與按鈕的報讀名稱，例如「附件」。 */
  readonly label = input('附件');
  readonly editable = input(false);
  /** 一個檔案一整列（唯讀的單據、詳情）；預設是依寬度並排的卡片（草稿、訊息）。 */
  readonly stacked = input(false);
  readonly remove = output<string>();
}

/** 檔案大小的顯示：1 MB 以上取一位小數，以下以 KB 計、至少 1 KB。 */
export function fileSize(bytes: number) {
  return bytes >= 1024 * 1024
    ? (bytes / 1024 / 1024).toFixed(1) + ' MB'
    : Math.max(1, Math.round(bytes / 1024)) + ' KB';
}
