import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { WorkspaceApi } from '@core/api/workspace-api';
import type {
  ConversationBackup,
  ConversationDto,
  ConversationSettingsRequest,
} from '@core/api/schema';
import { downloadFile } from '@shared/browser/download';
import { pickFile } from '@shared/browser/pick-file';
import { ToastService } from '@shared/ui/toast-service';
import { ProjectsApi } from '@features/work/projects/projects-api';
import { ConversationsApi } from '@features/work/conversations/conversations-api';
import type { ConversationAction } from '@features/work/conversations/conversation-actions';
import { ChatApi } from '../chat-api';
import { ChatStore } from '../chat-store';
import { routeOf } from '@core/api/catalog';

/**
 * 整理對話：重新命名、刪除、設定、專案、副本與備份。編輯會立即顯示在開啟中的對話與歷史列表，
 * 之後再與伺服器的版本一致，或復原；失敗則透過 ChatStore 回報。
 */
@Injectable({ providedIn: 'root' })
export class ChatConversations {
  private readonly store = inject(ChatStore);
  private readonly api = inject(WorkspaceApi);
  private readonly conversations = inject(ConversationsApi);
  private readonly chat = inject(ChatApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  /** 進行中的選單動作（副本、文字備份）；觸發它的選單鈕轉圈並擋下重複送出。 */
  readonly working = signal<{ id: string; action: ConversationAction } | null>(null);
  async assignProject(conversation: ConversationDto, projectId: string | null) {
    if (this.store.busy()) return false;
    const current = this.store.auth.guard();
    try {
      const value = await this.projectsApi.assign(conversation.id, projectId);
      if (!current()) return false;
      this.show(value);
      return true;
    } catch (error) {
      if (current()) this.store.report(error);
      return false;
    }
  }
  async rename(id: string, title: string) {
    const current = this.store.auth.guard();
    const previous = this.find(id);
    if (previous) this.show({ ...previous, title: title.trim() });
    try {
      const saved = await this.api.rename(id, title);
      if (!current()) return false;
      this.show(saved);
      return true;
    } catch (error) {
      if (!current()) return false;
      if (previous) this.show(previous);
      this.store.report(error);
      return false;
    }
  }
  async remove(id: string) {
    const current = this.store.auth.guard();
    try {
      await this.api.delete(id);
      if (!current()) return false;
      this.store.history.remove(id);
      if (this.store.selected()?.id === id) await this.router.navigate([routeOf('chat')]);
      return true;
    } catch (error) {
      if (current()) this.store.report(error);
      return false;
    }
  }
  async filterHistory(view = this.store.historyView(), label = this.store.historyLabel()) {
    this.store.historyView.set(view);
    this.store.historyLabel.set(label);
    try {
      await this.store.refreshHistory();
    } catch (error) {
      this.store.report(error);
    }
  }
  async organize(conversation: ConversationDto, settings: ConversationSettingsRequest) {
    const current = this.store.auth.guard();
    const changes = Object.fromEntries(
      Object.entries(settings).filter(([, value]) => value !== null && value !== undefined),
    );
    // 開啟中的對話副本比歷史列新，例如它的作用中分支。
    const previous = this.find(conversation.id) ?? conversation;
    this.show({ ...previous, ...changes });
    try {
      const saved = await this.conversations.settings(conversation.id, settings);
      if (!current()) return false;
      this.show(saved);
      if (settings.labels) {
        await this.store.history.reloadLabels();
        if (!current()) return false;
      }
      return true;
    } catch (error) {
      if (!current()) return false;
      this.show(previous);
      this.store.report(error);
      return false;
    }
  }
  async duplicate() {
    const current = this.store.auth.guard();
    const selected = this.store.selected();
    if (!selected || this.store.busy() || this.working()) return;
    this.working.set({ id: selected.id, action: 'duplicate' });
    try {
      const copy = await this.chat.duplicate(selected.id);
      if (!current()) return;
      await this.store.refreshHistory();
      if (!current()) return;
      await this.router.navigate([routeOf('chat'), copy.id]);
    } catch (error) {
      if (current()) this.store.report(error);
    } finally {
      this.working.set(null);
    }
  }
  async exportBackup() {
    const current = this.store.auth.guard();
    const selected = this.store.selected();
    if (!selected || this.store.busy() || this.working()) return;
    this.working.set({ id: selected.id, action: 'backup' });
    try {
      const backup = await this.chat.export(selected.id);
      if (!current()) return;
      downloadFile(JSON.stringify(backup, null, 2), selected.title, 'json');
    } catch (error) {
      if (current()) this.store.report(error);
    } finally {
      this.working.set(null);
    }
  }
  /** 要求備份檔；須在點擊中呼叫，瀏覽器才會允許開啟檔案選擇器。 */
  async chooseBackup() {
    const file = await pickFile('application/json,.json');
    if (file) await this.importBackup(file);
  }
  async importBackup(file: File) {
    const current = this.store.auth.guard();
    if (file.size > 8 * 1024 * 1024) {
      this.store.error.set('文字備份最多 8 MB。');
      return;
    }
    // 匯入從快捷指令發起，沒有按鈕可轉圈，所以進行中以提示表示；完成後換到匯入的對話即是結果。
    const progress = 'conversation-import';
    this.toast.show({ id: progress, tone: 'info', title: '正在匯入對話備份…', duration: 0 });
    try {
      const body = JSON.parse(await file.text()) as ConversationBackup;
      if (!current()) return;
      const imported = await this.conversations.import(body);
      if (!current()) return;
      await this.store.refreshHistory();
      await this.store.history.reloadLabels();
      if (!current()) return;
      await this.router.navigate([routeOf('chat'), imported.id]);
    } catch (error) {
      if (!current()) return;
      this.store.report(
        error instanceof SyntaxError ? new Error('備份不是有效的 JSON 格式。') : error,
      );
    } finally {
      this.toast.dismiss(progress);
    }
  }
  private find(id: string) {
    const selected = this.store.selected();
    return selected?.id === id
      ? selected
      : this.store.conversations().find((item) => item.id === id);
  }
  private show(conversation: ConversationDto) {
    if (this.store.selected()?.id === conversation.id) this.store.selected.set(conversation);
    this.store.history.place(conversation);
  }
}
