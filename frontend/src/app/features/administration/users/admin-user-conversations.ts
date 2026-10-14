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
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { RunTimingDisplay } from '@features/conversations/run-timing';
import { UserConversationReader } from './user-conversation-reader';

/**
 * The read-only conversations tab: a searchable list beside the conversation being read, each
 * scrolling on its own. Narrow screens show one at a time; choosing a conversation opens it and
 * its back button returns to the same row.
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
  /** On narrow screens the list gives way to the reading, so focus follows to its back button. */
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
