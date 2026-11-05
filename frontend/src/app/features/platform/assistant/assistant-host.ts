import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { AssistantApi } from './assistant-api';
import { assistantConversation } from './assistant-conversation';
import { AssistantPanel } from './assistant-panel';
import { AssistantPages } from './assistant-pages';

/**
 * 全站唯一的助理，由已登入的外框放置。所在頁面以 `AssistantPages.provide` 登記後才出現；
 * 對話跨頁保留在畫面上，但只有同一頁、同一組條件的輪次會當成上下文送出。
 */
@Component({
  selector: 'nx-assistant-host',
  imports: [AssistantPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (pages.current(); as page) {
    <nx-assistant-panel
      [conversation]="conversation"
      [page]="pageName()"
      [suggestions]="page.suggestions()"
    />
  }`,
})
export class AssistantHost {
  readonly pages = inject(AssistantPages);
  private readonly api = inject(AssistantApi);
  private readonly session = inject(WorkspaceSession);
  readonly pageName = computed(() => {
    const page = this.pages.current();
    return page ? this.session.featureName(page.feature) : null;
  });
  // 送出時才讀頁面目前的條件，提問一律對應畫面上已套用的那一組。
  readonly conversation = assistantConversation({
    ask: (question, signal) => {
      const page = this.pages.current();
      return this.api.ask(
        { ...question, page: page?.feature ?? null, view: page?.view() ?? null },
        signal,
      );
    },
    context: () => {
      const page = this.pages.current();
      return JSON.stringify([page?.feature ?? null, page?.view() ?? null]);
    },
  });
}
