import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { LibraryFileDto } from '@core/api/schema';
import { apiHref } from '@core/api/api-client';
import { ReaderLink } from '@shared/browser/reader-link';
import { Icon } from '@shared/ui/icon';
import { MenuPanel, type MenuAction } from '@shared/ui/menu-panel';
import { formatBytes, formatDate } from '@shared/browser/format';

const usageIcons: Record<string, string> = {
  chat: 'chat',
  projects: 'projects',
  knowledge: 'library',
};

/** 檔案庫中的檔案以磚塊呈現：預覽、名稱與其使用處；動作共用一個選單。 */
@Component({
  selector: 'nx-file-browser',
  imports: [ReaderLink, RouterLink, Icon, MenuPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './file-browser.scss',
  template: `@for (item of items(); track item.file.id) {
      <article class="file-card">
        <a
          class="file-cover"
          [nxReaderLink]="item.file.id"
          [readerAttachment]="true"
          [attr.aria-label]="'預覽 ' + item.file.fileName"
        >
          @if (item.file.isImage) {
            <img [src]="url(item.file.id)" alt="" loading="lazy" decoding="async" />
          } @else {
            <span class="file-extension">{{ extension(item.file.fileName) }}</span>
          }
        </a>
        <div class="file-info">
          <a
            class="file-name"
            [nxReaderLink]="item.file.id"
            [readerAttachment]="true"
            [title]="item.file.fileName"
            >{{ item.file.fileName }}</a
          >
          <span class="file-meta">{{ bytes(item.file.size) }} · {{ date(item.createdAt) }}</span>
          <span class="file-usages">
            @for (usage of item.usages.slice(0, 2); track usage.kind + usage.resourceId) {
              <a [routerLink]="['/' + usage.kind, usage.resourceId]" [title]="usage.name"
                ><nx-icon [name]="usageIcons[usage.kind] || 'document'" />{{ usage.name }}</a
              >
            } @empty {
              <span>尚未引用</span>
            }
            @if (item.usages.length > 2) {
              <span [title]="usageNames(item)">+{{ item.usages.length - 2 }}</span>
            }
          </span>
          <button
            #trigger
            type="button"
            class="icon-button file-menu"
            aria-haspopup="menu"
            [attr.aria-label]="'檔案操作：' + item.file.fileName"
            [attr.aria-expanded]="menu.isOpenFor(trigger)"
            [disabled]="busy()"
            (click)="open(item, trigger)"
          >
            <nx-icon name="more" />
          </button>
        </div>
      </article>
    }
    <nx-menu-panel #menu label="檔案操作" [items]="actions()" (action)="act($event)" />`,
})
export class FileBrowser {
  readonly items = input.required<LibraryFileDto[]>();
  readonly knowledge = input(false);
  readonly busy = input(false);
  readonly addKnowledge = output<LibraryFileDto>();
  readonly rename = output<LibraryFileDto>();
  readonly remove = output<LibraryFileDto>();
  readonly bytes = formatBytes;
  readonly usageIcons = usageIcons;
  readonly date = (value: string) => formatDate(value).split(' ')[0];
  private readonly menu = viewChild.required(MenuPanel);
  /** 選單開啟中的檔案；一個面板服務所有磚塊。 */
  private readonly chosen = signal<LibraryFileDto | null>(null);
  readonly actions = computed<MenuAction[]>(() => {
    const item = this.chosen();
    if (!item) return [];
    return [
      ...(this.knowledge() ? [{ id: 'knowledge', label: '加入知識庫', icon: 'library' }] : []),
      { id: 'rename', label: '重新命名', icon: 'edit' },
      { id: 'download', label: '下載原檔', icon: 'download' },
      {
        id: 'delete',
        label: item.canDelete ? '刪除檔案' : '刪除檔案（仍被引用）',
        icon: 'trash',
        danger: true,
        disabled: !item.canDelete,
      },
    ];
  });
  open(item: LibraryFileDto, trigger: HTMLElement) {
    if (this.menu().isOpenFor(trigger)) return this.menu().close();
    this.chosen.set(item);
    this.menu().open(trigger);
  }
  act(action: string) {
    const item = this.chosen();
    if (!item) return;
    if (action === 'knowledge') this.addKnowledge.emit(item);
    else if (action === 'rename') this.rename.emit(item);
    else if (action === 'delete') this.remove.emit(item);
    else if (action === 'download') {
      const link = document.createElement('a');
      link.href = this.url(item.file.id) + '?download=true';
      link.click();
    }
  }
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
