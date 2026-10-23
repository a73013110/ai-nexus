import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import type { ConversationDto } from '@core/api/schema';
import { SHORTCUTS } from '@core/layout/keyboard-shortcuts';
import { ViewMorph } from '@core/layout/view-morph';
import { currentDay } from '@shared/browser/calendar-day';
import { keyText } from '@shared/browser/hotkeys';
import { rovingIndex } from '@shared/browser/roving-index';
import { ViewScope } from '@shared/browser/view-scope';
import { Icon } from '@shared/ui/icon';
import { MenuPanel } from '@shared/ui/menu-panel';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { ViewSwitch } from '@shared/ui/view-switch';
import { NotificationStore } from '@features/platform/notifications/notification-store';
import {
  conversationMenu,
  type ConversationAction,
} from '@features/work/conversations/conversation-actions';
import { ChatConversations } from '../state/chat-conversations';
import { ChatStore } from '../chat-store';
import { historyGroups } from './history-groups';
import { routeOf } from '@core/api/catalog';

/**
 * 共用側邊欄中的聊天歷史。列是一般連結，共用同一個動作選單；瀏覽器會略過視口外列的渲染，
 * 因此很長的歷史仍維持輕量。
 */
@Component({
  selector: 'nx-chat-sidebar',
  imports: [RouterLink, FormField, Icon, Select, MenuPanel, PendingButton, ViewSwitch],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './chat-sidebar.scss',
  host: { style: 'display: contents' },
  templateUrl: './chat-sidebar.html',
})
export class ChatSidebar {
  readonly paletteKeys = keyText(SHORTCUTS.commandPalette.keys);
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly store = inject(ChatStore);
  readonly conversations = inject(ChatConversations);
  readonly notifications = inject(NotificationStore);
  private readonly morph = inject(ViewMorph);
  private readonly scope = inject(ViewScope);
  readonly action = output<{ conversation: ConversationDto; action: ConversationAction }>();
  readonly navigate = output<void>();
  readonly prompts = output<void>();
  readonly commands = output<void>();
  readonly searchModel = signal({ query: '' });
  readonly searchForm = form(this.searchModel, (schema) => maxLength(schema.query, 120));
  readonly labelOptions = computed(() => [
    { value: '', label: '全部標籤' },
    ...this.store.labels().map((value) => ({ value, label: value })),
  ]);
  readonly historyViews = [
    { value: 'active', label: '近期' },
    { value: 'favorites', label: '收藏' },
    { value: 'archived', label: '封存' },
  ];
  /** 輸入後還在防抖等待；與重讀合起來是搜尋框的轉圈。 */
  private readonly typing = signal(false);
  /** 搜尋、換檢視或標籤後的重讀（含防抖等待）。 */
  readonly searching = computed(() => this.typing() || this.store.history.refreshing());
  /** 還沒有任何列時讀取：畫骨架清單，不先說「沒有符合的對話」。 */
  readonly firstLoad = computed(
    () => (this.store.loading() || this.searching()) && !this.store.conversations().length,
  );
  /** 已有列時重讀：保留舊列並淡化。 */
  readonly listRefreshing = computed(
    () => (this.store.loading() || this.searching()) && this.store.conversations().length > 0,
  );
  /** 骨架列的寬度（%），長短交錯才像標題。 */
  protected readonly skeleton = [86, 64, 78, 52, 70, 60];
  private readonly today = currentDay();
  readonly groups = computed(() => historyGroups(this.store.conversations(), this.today()));
  readonly menu = viewChild.required(MenuPanel);
  /** 選單開啟中的那一列；項目會跟隨它最新的狀態與執行中的生成。 */
  readonly menuTarget = signal<string | null>(null);
  readonly menuItems = computed(() => {
    const conversation = this.store.conversations().find((item) => item.id === this.menuTarget());
    return conversation
      ? conversationMenu(conversation, {
          compact: true,
          sharing: this.store.hasSharing(),
          busy: this.store.liveRun()?.conversationId === conversation.id,
        })
      : [];
  });
  readonly menuLabel = computed(
    () =>
      '對話操作：' +
      (this.store.conversations().find((item) => item.id === this.menuTarget())?.title ?? ''),
  );
  /** 對話載入後，一般點擊會把列的標題 morph 到頂部列。 */
  open(event: MouseEvent, title: HTMLElement) {
    this.navigate.emit();
    if (!event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0)
      this.morph.arm('conversation-title', title);
  }
  searchChanged() {
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        void this.store.searchHistory(this.searchModel().query);
      },
      250,
      'search',
    );
  }
  selectLabel(value: string) {
    void this.conversations.filterHistory(undefined, value);
  }
  async loadMore() {
    if (this.store.history.loadingMore()) return;
    try {
      await this.store.refreshHistory(true);
    } catch (error) {
      this.store.report(error);
    }
  }
  menuOpen(id: string) {
    return this.menu().opened() && this.menuTarget() === id;
  }
  openMenu(event: Event, id: string) {
    const trigger = event.currentTarget as HTMLElement;
    if (this.menu().isOpenFor(trigger)) {
      this.menu().close();
      return;
    }
    this.menuTarget.set(id);
    this.menu().open(trigger);
  }
  chooseMenu(action: string) {
    const conversation = this.store.conversations().find((item) => item.id === this.menuTarget());
    if (conversation) this.action.emit({ conversation, action: action as ConversationAction });
  }
  /** 方向鍵在對話之間移動，如同列表；Tab 仍會逐列進入各自的選單。 */
  historyKey(event: KeyboardEvent, history: HTMLElement) {
    // 搜尋框只用 ↓ 進入列表，其他鍵留給輸入。
    const fromSearch = event.currentTarget !== history;
    if (fromSearch && event.key !== 'ArrowDown') return;
    const links = [...history.querySelectorAll<HTMLElement>('.history-row > a')];
    const current = links.indexOf(event.target as HTMLElement);
    if (!fromSearch && current < 0) return;
    const next = rovingIndex(event.key, current, links.length, { wrap: false });
    if (next === null) return;
    event.preventDefault();
    links[next]?.focus();
  }
}
