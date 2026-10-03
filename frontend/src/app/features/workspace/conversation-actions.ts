import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { Conversation } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { Disclosure } from '../../shared/ui/disclosure';

export type ConversationAction =
  | 'rename'
  | 'delete'
  | 'markdown'
  | 'backup'
  | 'settings'
  | 'duplicate'
  | 'favorite'
  | 'archive'
  | 'find';
@Component({
  selector: 'nx-conversation-actions',
  imports: [Icon, Disclosure],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
    <details class="conversation-menu" nxDisclosure>
      <summary class="icon-button" aria-label="對話操作"><nx-icon name="more" /></summary>
      <div class="floating-menu">
        <button type="button" data-close (click)="action.emit('settings')">
          <nx-icon name="sliders" />對話指令與標籤
        </button>
        <button type="button" data-close (click)="action.emit('rename')">
          <nx-icon name="edit" />重新命名目前對話
        </button>
        <button type="button" data-close [disabled]="busy()" (click)="action.emit('duplicate')">
          <nx-icon name="copy" />建立對話副本
        </button>
        <button type="button" data-close [disabled]="busy()" (click)="action.emit('archive')">
          <nx-icon [name]="conversation().isArchived ? 'restore' : 'archive'" />{{
            conversation().isArchived ? '還原封存對話' : '封存對話'
          }}
        </button>
        <hr />
        <button
          type="button"
          data-close
          [disabled]="busy()"
          aria-label="匯出目前分支為 Markdown"
          (click)="action.emit('markdown')"
        >
          <nx-icon name="download" />匯出 Markdown
        </button>
        <button type="button" data-close [disabled]="busy()" (click)="action.emit('backup')">
          <nx-icon name="document" />匯出 JSON 文字備份
        </button>
        <hr />
        <button
          type="button"
          data-close
          [disabled]="busy()"
          class="danger-text"
          aria-label="刪除目前對話"
          (click)="action.emit('delete')"
        >
          <nx-icon name="trash" />刪除目前對話
        </button>
      </div>
    </details>
  </div>`,
})
export class ConversationActions {
  readonly conversation = input.required<Conversation>();
  readonly busy = input(false);
  readonly action = output<ConversationAction>();
}
