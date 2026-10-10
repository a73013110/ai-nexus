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
@Component({
  selector: 'nx-conversation-actions',
  host: { '[class.is-compact]': 'compact()' },
  imports: [Icon, ActionMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './conversation-actions.scss',
  template: `<div class="conversation-actions">
    @if (!compact()) {
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
    }
    <nx-action-menu
      [label]="compact() ? '對話操作：' + conversation().title : '對話操作'"
      [items]="items()"
      (action)="choose($event)"
    />
  </div>`,
})
export class ConversationActions {
  readonly conversation = input.required<ConversationDto>();
  readonly busy = input(false);
  readonly compact = input(false);
  readonly sharing = input(false);
  readonly action = output<ConversationAction>();
  readonly items = computed<MenuAction[]>(() => [
    ...(this.sharing()
      ? [{ id: 'share', label: '分享', icon: 'share', disabled: this.busy() }]
      : []),
    { id: 'rename', label: this.compact() ? '重新命名' : '重新命名目前對話', icon: 'edit' },
    { id: 'favorite', label: this.conversation().isFavorite ? '取消收藏' : '收藏', icon: 'star' },
    {
      id: 'archive',
      label: this.conversation().isArchived ? '還原封存對話' : '封存對話',
      icon: this.conversation().isArchived ? 'restore' : 'archive',
      disabled: this.busy(),
    },
    ...(!this.compact()
      ? [
          { id: 'settings', label: '對話指令與標籤', icon: 'sliders' },
          { id: 'duplicate', label: '建立對話副本', icon: 'copy', disabled: this.busy() },
          { id: 'markdown', label: '匯出 Markdown', icon: 'download', disabled: this.busy() },
          { id: 'backup', label: '匯出 JSON 文字備份', icon: 'document', disabled: this.busy() },
        ]
      : []),
    {
      id: 'delete',
      label: this.compact() ? '刪除對話' : '刪除目前對話',
      icon: 'trash',
      danger: true,
      disabled: this.busy(),
    },
  ]);
  choose(value: string) {
    this.action.emit(value as ConversationAction);
  }
}
