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
import { catalogFeature } from '@core/api/catalog';
import type { LibraryFileDto } from '@core/api/schema';
import { ReaderLink } from '@shared/browser/reader-link';
import { Icon } from '@shared/ui/icon';
import { MenuPanel, type MenuAction } from '@shared/ui/menu-panel';
import { formatBytes, formatDay } from '@shared/browser/format';
import { attachmentContent } from '../files-api';

/**
 * 引用處的種類（`chat`、`projects`、`knowledge`）就是功能目錄的頁面 ID；網址與圖示都由目錄取得，
 * 不認得的種類只顯示名稱、不給連結。
 */
function usagePage(kind: string) {
  const feature = catalogFeature(kind);
  return feature?.kind === 'page' ? feature : null;
}

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
              @if (usagePage(usage.kind); as page) {
                <a [routerLink]="[page.route, usage.resourceId]" [title]="usage.name"
                  ><nx-icon [name]="page.icon" />{{ usage.name }}</a
                >
              } @else {
                <span [title]="usage.name"><nx-icon name="document" />{{ usage.name }}</span>
              }
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
            [attr.aria-busy]="working() === item.file.id || null"
            [attr.aria-disabled]="working() === item.file.id || null"
            (click)="working() === item.file.id || open(item, trigger)"
          >
            <nx-icon [name]="working() === item.file.id ? 'loading' : 'more'" />
          </button>
        </div>
      </article>
    }
    <nx-menu-panel #menu label="檔案操作" [items]="actions()" (action)="act($event)" />`,
})
export class FileBrowser {
  readonly items = input.required<readonly LibraryFileDto[]>();
  readonly knowledge = input(false);
  /** 動作進行中的檔案 id（例如刪除）：只有那張卡片的選單鈕轉圈並擋下再次開啟。 */
  readonly working = input<string | null>(null);
  readonly addKnowledge = output<LibraryFileDto>();
  readonly rename = output<LibraryFileDto>();
  readonly remove = output<LibraryFileDto>();
  readonly bytes = formatBytes;
  readonly usagePage = usagePage;
  readonly date = formatDay;
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
  readonly url = attachmentContent;
  extension(name: string) {
    return name.split('.').pop()?.toUpperCase() || 'FILE';
  }
  usageNames(item: LibraryFileDto) {
    return item.usages.map((value) => value.name).join('、');
  }
}
