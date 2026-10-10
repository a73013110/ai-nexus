import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NexusApi } from '../../core/api/nexus-api';
import type {
  ConversationBackup,
  ConversationDto,
  ConversationSettingsRequest,
} from '../../core/api/schema';
import { downloadFile } from '../../shared/browser/download';
import { ProjectsApi } from '../projects/projects-api';
import { WorkspaceApi } from '../workspace/workspace-api';
import { ChatStore } from './chat-store';

/**
 * Organizing conversations: rename, delete, settings, project, copies and backups. Each write
 * refreshes the history list and reports failures through ChatStore.
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
      if (this.store.selected()?.id === value.id) this.store.selected.set(value);
      await this.store.refreshHistory();
      return true;
    } catch (error) {
      this.store.report(error);
      return false;
    }
  }
  async rename(id: string, title: string) {
    try {
      const updated = await this.api.rename(id, title);
      if (this.store.selected()?.id === id) this.store.selected.set(updated);
      await this.store.refreshHistory();
      return true;
    } catch (error) {
      this.store.report(error);
      return false;
    }
  }
  async remove(id: string) {
    try {
      await this.api.delete(id);
      if (this.store.selected()?.id === id) await this.router.navigate(['/chat']);
      await this.store.refreshHistory();
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
    try {
      const saved = await this.workspace.settings(conversation.id, settings);
      if (generation !== this.store.auth.generation()) return false;
      if (this.store.selected()?.id === saved.id) this.store.selected.set(saved);
      await this.store.refreshHistory();
      const labels = await this.workspace.labels();
      if (generation !== this.store.auth.generation()) return false;
      this.store.labels.set(labels);
      return true;
    } catch (error) {
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
}
