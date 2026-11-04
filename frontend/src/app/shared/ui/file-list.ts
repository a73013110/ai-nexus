import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
  signal,
  type WritableSignal,
} from '@angular/core';
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
  /** 小尺寸的縮圖網址（附件的 `thumbnail` 端點）；不要放原檔，一張照片就是幾 MB。 */
  thumbnail?: string | null;
}

/**
 * - `cards`：依寬度並排的卡片（草稿）。
 * - `tiles`：訊息裡的方塊，圖片只顯示縮圖、其他檔案顯示類型與名稱，下載鈕在指到時浮現。
 * - `rows`：一個檔案一整列（唯讀的單據、詳情）。
 */
export type FileListLayout = 'cards' | 'tiles' | 'rows';

/**
 * 檔案清單：點檔案在閱讀器預覽，另有下載原檔，`editable` 時多一個移除鈕。
 * 草稿與訊息的附件、公文系統表單的附件共用；資料來源不同，只差在呼叫端怎麼組 {@link FileListItem}。
 * 縮圖讀不到（檔案已清除、格式無法解碼）時改成檔案類型，不顯示破圖。
 */
@Component({
  selector: 'nx-file-list',
  imports: [Icon, ReaderLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-layout]': 'layout()' },
  styleUrls: ['./file-list.scss', './file-tiles.scss'],
  template: `<ul class="attachment-list" [attr.aria-label]="label()">
    @for (file of files(); track file.key; let index = $index) {
      @let image = !!file.thumbnail && !failed().has(file.key);
      <li
        class="attachment-card"
        [class.is-image]="image"
        [style.--i]="index"
        animate.enter="attachment-enter"
      >
        <a
          class="attachment-preview"
          [nxReaderLink]="file.reader.id"
          [readerAttachment]="file.reader.attachment"
          [readerScope]="file.reader.scope ?? null"
          [readerRemote]="file.reader.remote ?? null"
          [attr.aria-label]="'閱讀' + label() + '：' + file.fileName"
          [title]="file.fileName"
        >
          @if (image) {
            <img
              [src]="file.thumbnail"
              alt=""
              width="72"
              height="72"
              loading="lazy"
              decoding="async"
              [class.is-loaded]="loaded().has(file.key)"
              (load)="settle(loaded, file.key)"
              (error)="settle(failed, file.key)"
            />
          } @else {
            <span class="attachment-symbol" aria-hidden="true"
              ><nx-icon name="document" /><span class="attachment-kind">{{
                kind(file.fileName)
              }}</span></span
            >
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
  readonly layout = input<FileListLayout>('cards');
  readonly remove = output<string>();
  /** 縮圖已載入（淡入）與載入失敗（改顯示類型）的檔案。 */
  readonly loaded = signal<ReadonlySet<string>>(new Set());
  readonly failed = signal<ReadonlySet<string>>(new Set());
  readonly kind = fileKind;
  settle(set: WritableSignal<ReadonlySet<string>>, key: string) {
    if (!set().has(key)) set.update((keys) => new Set(keys).add(key));
  }
}

/** 檔案大小的顯示：1 MB 以上取一位小數，以下以 KB 計、至少 1 KB。 */
export function fileSize(bytes: number) {
  return bytes >= 1024 * 1024
    ? (bytes / 1024 / 1024).toFixed(1) + ' MB'
    : Math.max(1, Math.round(bytes / 1024)) + ' KB';
}

/** 方塊上的檔案類型：副檔名大寫（最多 4 個字），沒有或太長時寫「檔案」。 */
export function fileKind(fileName: string) {
  const extension = /\.([a-z0-9]{1,4})$/i.exec(fileName)?.[1];
  return extension ? extension.toUpperCase() : '檔案';
}
