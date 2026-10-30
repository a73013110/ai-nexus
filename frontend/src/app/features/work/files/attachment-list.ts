import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { AttachmentDto } from '@core/api/schema';
import { attachmentContent } from './files-api';
import { scopedFileHref, type FileScope } from '@shared/browser/reader-overlay';
import { FileList, fileSize, type FileListItem } from '@shared/ui/file-list';

/**
 * 草稿與訊息的附件：把檔案庫的附件轉成共用的 {@link FileList}。
 * 有 `scope` 時檔案由容器（分享、稽核檢視的對話）授權，縮圖、下載與閱讀器都走容器的路由。
 */
@Component({
  selector: 'nx-attachment-list',
  imports: [FileList],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-file-list
    [files]="items()"
    [editable]="editable()"
    (remove)="remove.emit($event)"
  />`,
})
export class AttachmentList {
  readonly files = input.required<AttachmentDto[]>();
  readonly editable = input(false);
  readonly scope = input<FileScope | null>(null);
  readonly remove = output<string>();
  readonly items = computed(() =>
    this.files().map((file): FileListItem => {
      const url = this.url(file.id);
      return {
        key: file.id,
        fileName: file.fileName,
        detail: `${fileSize(file.size)} · ${
          file.analysisMode === 'ocr-required'
            ? '等待文字辨識'
            : file.isImage
              ? '圖片分析'
              : '文字分析'
        }`,
        reader: { id: file.id, attachment: true, scope: this.scope() },
        downloadUrl: url + '?download=true',
        thumbnail: file.isImage ? url : null,
      };
    }),
  );
  private url(id: string) {
    const scope = this.scope();
    return scope ? scopedFileHref(scope, id) : attachmentContent(id);
  }
}
