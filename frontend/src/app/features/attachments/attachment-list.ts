import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { Attachment } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';

@Component({
  selector: 'nx-attachment-list',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ul class="attachment-list" aria-label="附件">
    @for (file of files(); track file.id) {
      <li class="attachment-card" animate.enter="attachment-enter">
        <a
          [href]="url(file.id)"
          target="_blank"
          rel="noopener"
          [attr.aria-label]="(file.isImage ? '預覽圖片：' : '下載文件：') + file.fileName"
        >
          @if (file.isImage) {
            <img [src]="url(file.id)" [alt]="file.fileName" width="48" height="48" loading="lazy" />
          } @else {
            <span class="attachment-symbol"><nx-icon name="document" /></span>
          }
          <span
            ><strong>{{ file.fileName }}</strong
            ><small
              >{{ size(file.size) }} · {{ file.isImage ? '圖片分析' : '文字分析' }}</small
            ></span
          >
        </a>
        @if (editable()) {
          <button
            type="button"
            class="icon-button"
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
