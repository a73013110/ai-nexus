import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { Attachment } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { ReaderLink } from '../../shared/browser/reader-link';

@Component({
  selector: 'nx-attachment-list',
  imports: [Icon, ReaderLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ul class="attachment-list" aria-label="附件">
    @for (file of files(); track file.id) {
      <li class="attachment-card" animate.enter="attachment-enter">
        <a
          class="attachment-preview"
          [nxReaderLink]="file.id"
          [readerAttachment]="true"
          [attr.aria-label]="'閱讀附件：' + file.fileName"
          [title]="file.fileName"
        >
          @if (file.isImage) {
            <img [src]="url(file.id)" [alt]="file.fileName" width="48" height="48" loading="lazy" />
          } @else {
            <span class="attachment-symbol"><nx-icon name="document" /></span>
          }
          <span class="attachment-copy"
            ><strong>{{ file.fileName }}</strong
            ><small
              >{{ size(file.size) }} ·
              {{
                file.analysisMode === 'ocr-required'
                  ? '等待文字辨識'
                  : file.isImage
                    ? '圖片分析'
                    : '文字分析'
              }}</small
            ></span
          >
          <nx-icon class="attachment-open" name="chevron" />
        </a>
        <a
          class="icon-button attachment-download"
          [href]="url(file.id) + '?download=true'"
          [attr.aria-label]="'下載附件：' + file.fileName"
          title="下載原檔"
          ><nx-icon name="download"
        /></a>
        @if (editable()) {
          <button
            type="button"
            class="icon-button attachment-remove"
            [attr.aria-label]="'移除附件：' + file.fileName"
            (click)="remove.emit(file.id)"
          >
            <nx-icon name="close" />
          </button>
        }
      </li>
    }
  </ul>`,
})
export class AttachmentList {
  readonly files = input.required<Attachment[]>();
  readonly editable = input(false);
  readonly remove = output<string>();
  url(id: string) {
    return `/api/v1/attachments/${encodeURIComponent(id)}/content`;
  }
  size(bytes: number) {
    return bytes >= 1024 * 1024
      ? (bytes / 1024 / 1024).toFixed(1) + ' MB'
      : Math.max(1, Math.round(bytes / 1024)) + ' KB';
  }
}
