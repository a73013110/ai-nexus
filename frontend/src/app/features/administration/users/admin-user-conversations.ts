import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  viewChild,
} from '@angular/core';
import { below } from '@shared/browser/breakpoints';
import { formatBytes, formatDate, formatNumber } from '@shared/browser/format';
import { mediaQuery } from '@shared/browser/media-query';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { Checkbox } from '@shared/ui/checkbox';
import { TablePagination } from '@shared/ui/data-table';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { RunTimingDisplay } from '@features/conversations/run-timing';
import { UserConversationReader } from './user-conversation-reader';

/**
 * 唯讀的對話分頁：左側是可搜尋的列表，右側是正在閱讀的對話，兩者各自捲動。窄螢幕一次只顯示一個；
 * 選取對話會開啟它，而其返回鈕會回到同一列。
 */
@Component({
  selector: 'nx-admin-user-conversations',
  imports: [
    Checkbox,
    EmptyState,
    Icon,
    MarkdownView,
    Notice,
    RunTimingDisplay,
    SearchField,
    StatusBadge,
    TablePagination,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-user-conversations.scss',
  templateUrl: './admin-user-conversations.html',
})
export class AdminUserConversations {
  readonly reader = inject(UserConversationReader);
  readonly date = formatDate;
  readonly format = formatNumber;
  readonly bytes = formatBytes;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);
  private readonly narrow = mediaQuery(below('narrow'));
  private readonly backButton = viewChild.required<ElementRef<HTMLButtonElement>>('back');
  constructor() {
    this.reader.start();
  }
  /** 窄螢幕上列表讓位給閱讀區，因此焦點會移到其返回鈕。 */
  open(id: string) {
    this.reader.read(id);
    if (this.narrow())
      afterNextRender(() => this.backButton().nativeElement.focus(), { injector: this.injector });
  }
  close() {
    const id = this.reader.selected();
    this.reader.close();
    afterNextRender(
      () =>
        this.host
          .querySelector<HTMLElement>(`.conversation-item[data-conversation="${id}"]`)
          ?.focus(),
      { injector: this.injector },
    );
  }
}
