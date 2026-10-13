import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { formatBytes, formatDate, formatNumber } from '../../../shared/browser/format';
import { MarkdownView } from '../../../shared/markdown/markdown-view';
import { Checkbox } from '../../../shared/ui/checkbox';
import { EmptyState } from '../../../shared/ui/empty-state';
import { Icon } from '../../../shared/ui/icon';
import { Notice } from '../../../shared/ui/notice';
import { SearchField } from '../../../shared/ui/search-field';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { RunTimingDisplay } from '../../workspace/run-timing';
import { UserConversationReader } from './user-conversation-reader';

/** The read-only conversations tab: a searchable list beside the conversation being read. */
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
  constructor() {
    this.reader.start();
  }
}
