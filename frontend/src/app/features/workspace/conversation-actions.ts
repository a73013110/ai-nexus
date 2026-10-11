import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { ConversationDto } from '../../core/api/schema';
import { Icon } from '../../shared/ui/icon';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';

export type ConversationAction =
  | 'rename'
  | 'delete'
  | 'markdown'
  | 'backup'
  | 'settings'
  | 'duplicate'
  | 'favorite'
  | 'archive'
  | 'find'
  | 'share';

/** Menu items for one conversation; `compact` is the shorter list used by sidebar rows. */
export function conversationMenu(
  conversation: ConversationDto,
  { busy = false, compact = false, sharing = false } = {},
): MenuAction[] {
  return [
    ...(sharing ? [{ id: 'share', label: '分享', icon: 'share', disabled: busy }] : []),
    { id: 'rename', label: compact ? '重新命名' : '重新命名目前對話', icon: 'edit' },
    { id: 'favorite', label: conversation.isFavorite ? '取消收藏' : '收藏', icon: 'star' },
    {
      id: 'archive',
      label: conversation.isArchived ? '還原封存對話' : '封存對話',
      icon: conversation.isArchived ? 'restore' : 'archive',
      disabled: busy,
    },
    ...(!compact
      ? [
          { id: 'settings', label: '對話指令與標籤', icon: 'sliders' },
          { id: 'duplicate', label: '建立對話副本', icon: 'copy', disabled: busy },
          { id: 'markdown', label: '匯出 Markdown', icon: 'download', disabled: busy },
          { id: 'backup', label: '匯出 JSON 文字備份', icon: 'document', disabled: busy },
        ]
      : []),
    {
      id: 'delete',
      label: compact ? '刪除對話' : '刪除目前對話',
      icon: 'trash',
      danger: true,
      disabled: busy,
    },
  ];
}

@Component({
  selector: 'nx-conversation-actions',
  imports: [Icon, ActionMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './conversation-actions.scss',
  template: `<div class="conversation-actions">
    <button
      type="button"
      class="icon-button favorite-button"
      [class.is-favorite]="conversation().isFavorite"
      [attr.aria-label]="conversation().isFavorite ? '取消收藏目前對話' : '收藏目前對話'"
      [attr.aria-pressed]="conversation().isFavorite"
      (click)="action.emit('favorite')"
    >
      <nx-icon name="star" />
    </button>
    <button
      type="button"
      class="icon-button"
      aria-label="搜尋目前對話訊息"
      title="搜尋訊息"
      (click)="action.emit('find')"
    >
      <nx-icon name="search" />
    </button>
    <nx-action-menu label="對話操作" [items]="items()" (action)="choose($event)" />
  </div>`,
})
export class ConversationActions {
  readonly conversation = input.required<ConversationDto>();
  readonly busy = input(false);
  readonly sharing = input(false);
  readonly action = output<ConversationAction>();
  readonly items = computed(() =>
    conversationMenu(this.conversation(), {
      busy: this.busy(),
      sharing: this.sharing(),
    }),
  );
  choose(value: string) {
    this.action.emit(value as ConversationAction);
  }
}
