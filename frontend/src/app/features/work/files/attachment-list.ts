import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { AttachmentDto } from '@core/api/schema';
import { apiHref } from '@core/api/api-client';
import { attachmentContent } from './files-api';
import { FileList, fileSize, type FileListItem } from '@shared/ui/file-list';

/** 草稿與訊息的附件：把檔案庫的附件（或分享裡的檔案）轉成共用的 {@link FileList}。 */
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
  readonly shareId = input<string | null>(null);
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
        reader: { id: file.id, attachment: true, shareId: this.shareId() },
        downloadUrl: url + '?download=true',
        thumbnail: file.isImage ? url : null,
      };
    }),
  );
  private url(id: string) {
    const share = this.shareId();
    return share
      ? apiHref('/api/v1/shares/{id}/files/{file}', { path: { id: share, file: id } })
      : attachmentContent(id);
  }
}
