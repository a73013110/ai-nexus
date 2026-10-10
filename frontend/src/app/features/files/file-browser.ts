import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { LibraryFileDto } from '../../core/api/schema';
import { apiHref } from '../../core/api/api-client';
import { ReaderLink } from '../../shared/browser/reader-link';
import { Icon } from '../../shared/ui/icon';
import { Card } from '../../shared/ui/card';
import { formatBytes, formatDate } from '../../shared/browser/format';

@Component({
  selector: 'nx-file-browser',
  imports: [ReaderLink, RouterLink, Icon, Card],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="file-browser" [class.file-browser-list]="layout() === 'list'">
    @for (item of items(); track item.file.id) {
      <article nxCard class="file-card">
        <a
          class="file-cover"
          [nxReaderLink]="item.file.id"
          [readerAttachment]="true"
          [attr.aria-label]="'預覽 ' + item.file.fileName"
        >
          @if (item.file.isImage) {
            <img
              [src]="url(item.file.id)"
              [alt]="item.file.fileName"
              loading="lazy"
              decoding="async"
            />
          } @else {
            <span class="file-cover-document"
              ><nx-icon name="document" /><span>{{ extension(item.file.fileName) }}</span></span
            >
          }
          <span class="file-cover-preview"><nx-icon name="eye" />預覽</span>
        </a>
        <div class="file-card-body">
          <a
            class="file-name"
            [nxReaderLink]="item.file.id"
            [readerAttachment]="true"
            [title]="item.file.fileName"
            >{{ item.file.fileName }}</a
          >
          <p class="file-meta">
            {{ bytes(item.file.size) }}<span>·</span>{{ date(item.createdAt) }}
          </p>
          <div class="file-usages">
            @for (usage of item.usages.slice(0, 2); track usage.kind + usage.resourceId) {
              <a
                [routerLink]="
                  usage.kind === 'chat'
                    ? ['/chat', usage.resourceId]
                    : usage.kind === 'projects'
                      ? ['/projects', usage.resourceId]
                      : ['/knowledge']
                "
                [queryParams]="usage.kind === 'knowledge' ? { collection: usage.resourceId } : null"
                [title]="usage.name"
              >
                <nx-icon
                  [name]="
                    usage.kind === 'chat'
                      ? 'lines'
                      : usage.kind === 'projects'
                        ? 'projects'
                        : 'library'
                  "
                />{{ usage.name }}
              </a>
            } @empty {
              <span class="file-unused">{{
                item.canDelete ? '檔案庫保存' : '已被工作區引用'
              }}</span>
            }
            @if (item.usages.length > 2) {
              <span class="file-unused" [title]="usageNames(item)"
                >+{{ item.usages.length - 2 }} 處使用</span
              >
            }
          </div>
        </div>
        <div class="file-card-actions">
          @if (selectable()) {
            <button
              class="secondary-button"
              (click)="chosen.emit(item)"
              [attr.aria-label]="'選取 ' + item.file.fileName"
            >
              <nx-icon name="plus" />選取
            </button>
          } @else {
            @if (knowledge()) {
              <button
                class="quiet-button file-add-knowledge"
                (click)="addKnowledge.emit(item)"
                [attr.aria-label]="'將 ' + item.file.fileName + ' 加入知識庫'"
              >
                <nx-icon name="library" />加入知識庫
              </button>
            }
            <button
              class="icon-button"
              [disabled]="busy()"
              [attr.aria-label]="'重新命名 ' + item.file.fileName"
              title="重新命名檔案"
              (click)="rename.emit(item)"
            >
              <nx-icon name="edit" />
            </button>
            <a
              class="icon-button"
              [href]="url(item.file.id) + '?download=true'"
              [attr.aria-label]="'下載 ' + item.file.fileName"
              title="下載原檔"
              ><nx-icon name="download"
            /></a>
            <button
              class="icon-button danger-text"
              [disabled]="busy() || !item.canDelete"
              [title]="
                item.canDelete ? '刪除檔案' : '仍被對話、知識庫或專案引用；先移除引用後即可刪除'
              "
              [attr.aria-label]="'刪除 ' + item.file.fileName"
              (click)="remove.emit(item)"
            >
              <nx-icon name="trash" />
            </button>
          }
        </div>
      </article>
    }
  </div>`,
})
export class FileBrowser {
  readonly items = input.required<LibraryFileDto[]>();
  readonly layout = input('grid');
  readonly selectable = input(false);
  readonly knowledge = input(false);
  readonly busy = input(false);
  readonly chosen = output<LibraryFileDto>();
  readonly addKnowledge = output<LibraryFileDto>();
  readonly rename = output<LibraryFileDto>();
  readonly remove = output<LibraryFileDto>();
  readonly bytes = formatBytes;
  readonly date = (value: string) => formatDate(value).split(' ')[0];
  url(id: string) {
    return apiHref('/api/v1/attachments/{id}/content', { path: { id } });
  }
  extension(name: string) {
    return name.split('.').pop()?.toUpperCase() || 'FILE';
  }
  usageNames(item: LibraryFileDto) {
    return item.usages.map((value) => value.name).join('、');
  }
}
