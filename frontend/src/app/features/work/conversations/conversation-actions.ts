import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { ConversationDto } from '@core/api/schema';
import { Icon } from '@shared/ui/icon';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';

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
  | 'share'
  | 'spend'
  | 'project';

/** 單一對話的選單項目；`compact` 是側邊欄列所用的較短清單。 */
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
      label="對話操作"
      [items]="items()"
      [busy]="!!working()"
      (action)="choose($event)"
    />
  </div>`,
})
export class ConversationActions {
  readonly conversation = input.required<ConversationDto>();
  readonly busy = input(false);
  /** 正在處理的選單動作（例如 `backup`）：選單鈕轉圈、該項目標示進行中，完成前不再開啟。 */
  readonly working = input<ConversationAction | null>(null);
  readonly sharing = input(false);
  /** 手機只保留選單：搜尋、專案連結與花費（`spend` 標籤）會併入其中。 */
  readonly compact = input(false);
  readonly spend = input<string | null>(null);
  readonly action = output<ConversationAction>();
  readonly items = computed(() => {
    const working = this.working();
    const items = conversationMenu(this.conversation(), {
      busy: this.busy(),
      sharing: this.sharing(),
    }).map((item) => (item.id === working ? { ...item, busy: true } : item));
    if (!this.compact()) return items;
    const spend = this.spend();
    const at = items.findIndex((item) => item.id === 'favorite') + 1;
    return [
      ...items.slice(0, at),
      { id: 'find', label: '搜尋目前對話訊息', icon: 'search' },
      ...(spend ? [{ id: 'spend', label: spend, icon: 'money' }] : []),
      ...(this.conversation().projectId
        ? [{ id: 'project', label: '檢視專案指示與參考文件', icon: 'archive' }]
        : []),
      ...items.slice(at),
    ];
  });
  choose(value: string) {
    this.action.emit(value as ConversationAction);
  }
}
