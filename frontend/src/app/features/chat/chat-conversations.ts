import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NexusApi } from '../../core/api/nexus-api';
import type {
  ConversationBackup,
  ConversationDto,
  ConversationSettingsRequest,
} from '../../core/api/schema';
import { downloadFile } from '../../shared/browser/download';
import { pickFile } from '../../shared/browser/pick-file';
import { ProjectsApi } from '../projects/projects-api';
import { WorkspaceApi } from '../workspace/workspace-api';
import { ChatStore } from './chat-store';

/**
 * Organizing conversations: rename, delete, settings, project, copies and backups. Edits show at
 * once in the open conversation and the history list, then settle to the server's copy or roll
 * back; failures are reported through ChatStore.
 */
@Injectable({ providedIn: 'root' })
export class ChatConversations {
  private readonly store = inject(ChatStore);
  private readonly api = inject(NexusApi);
  private readonly workspace = inject(WorkspaceApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly router = inject(Router);
  async assignProject(conversation: ConversationDto, projectId: string | null) {
    if (this.store.busy()) return false;
    const generation = this.store.auth.generation();
    try {
      const value = await this.projectsApi.assign(conversation.id, projectId);
      if (generation !== this.store.auth.generation()) return false;
      this.show(value);
      return true;
    } catch (error) {
      this.store.report(error);
      return false;
    }
  }
  async rename(id: string, title: string) {
    const current = this.find(id);
    if (current) this.show({ ...current, title: title.trim() });
    try {
      this.show(await this.api.rename(id, title));
      return true;
    } catch (error) {
      if (current) this.show(current);
      this.store.report(error);
      return false;
    }
  }
  async remove(id: string) {
    try {
      await this.api.delete(id);
      this.store.history.remove(id);
      if (this.store.selected()?.id === id) await this.router.navigate(['/chat']);
      return true;
    } catch (error) {
      this.store.report(error);
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
    const generation = this.store.auth.generation();
    const changes = Object.fromEntries(
      Object.entries(settings).filter(([, value]) => value !== null && value !== undefined),
    );
    // The open conversation's copy is newer than a history row, e.g. its active branch.
    const current = this.find(conversation.id) ?? conversation;
    this.show({ ...current, ...changes });
    try {
      const saved = await this.workspace.settings(conversation.id, settings);
      if (generation !== this.store.auth.generation()) return false;
      this.show(saved);
      if (settings.labels) {
        const labels = await this.workspace.labels();
        if (generation !== this.store.auth.generation()) return false;
        this.store.labels.set(labels);
      }
      return true;
    } catch (error) {
      if (generation === this.store.auth.generation()) this.show(current);
      this.store.report(error);
      return false;
    }
  }
  async duplicate() {
    const generation = this.store.auth.generation();
    const selected = this.store.selected();
    if (!selected || this.store.busy()) return;
    try {
      const copy = await this.workspace.duplicate(selected.id);
      if (generation !== this.store.auth.generation()) return;
      await this.store.refreshHistory();
      await this.router.navigate(['/chat', copy.id]);
    } catch (error) {
      this.store.report(error);
    }
  }
  async exportBackup() {
    const generation = this.store.auth.generation();
    const selected = this.store.selected();
    if (!selected || this.store.busy()) return;
    try {
      const backup = await this.workspace.export(selected.id);
      if (generation !== this.store.auth.generation()) return;
      downloadFile(JSON.stringify(backup, null, 2), selected.title, 'json');
    } catch (error) {
      this.store.report(error);
    }
  }
  /** Asks for a backup file; call from a click so the browser allows the file chooser. */
  async chooseBackup() {
    const file = await pickFile('application/json,.json');
    if (file) await this.importBackup(file);
  }
  async importBackup(file: File) {
    const generation = this.store.auth.generation();
    if (file.size > 8 * 1024 * 1024) {
      this.store.error.set('文字備份最多 8 MB。');
      return;
    }
    try {
      const body = JSON.parse(await file.text()) as ConversationBackup;
      if (generation !== this.store.auth.generation()) return;
      const imported = await this.workspace.import(body);
      if (generation !== this.store.auth.generation()) return;
      await this.store.refreshHistory();
      const labels = await this.workspace.labels();
      if (generation !== this.store.auth.generation()) return;
      this.store.labels.set(labels);
      await this.router.navigate(['/chat', imported.id]);
    } catch (error) {
      this.store.report(
        error instanceof SyntaxError ? new Error('備份不是有效的 JSON 格式。') : error,
      );
    }
  }
  private find(id: string) {
    const selected = this.store.selected();
    return selected?.id === id ? selected : this.store.conversations().find((x) => x.id === id);
  }
  private show(conversation: ConversationDto) {
    if (this.store.selected()?.id === conversation.id) this.store.selected.set(conversation);
    this.store.history.place(conversation);
  }
}
