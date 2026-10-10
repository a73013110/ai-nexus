import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  inject,
  signal,
  viewChild,
  computed,
} from '@angular/core';
import { FormField, form, maxLength } from '@angular/forms/signals';
import type { ConversationDto, ConversationSettingsRequest } from '../../core/api/schema';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { ProjectsApi } from '../projects/projects-api';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';

import { ChatConversations } from '../chat/chat-conversations';
@Component({
  selector: 'nx-conversation-settings',
  imports: [Notice, CompactDialog, Field, Icon, FormField, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './conversation-settings-dialog.scss',
  template: ` <dialog
    nxCompactDialog
    #dialog
    class="workspace-dialog settings-dialog"
    aria-labelledby="settings-title"
    (cancel)="busy() && $event.preventDefault()"
  >
    <div class="dialog-scroll">
      <form (submit)="submit($event)">
        <div class="dialog-heading">
          <div>
            <span class="panel-eyebrow">CONVERSATION SETTINGS</span>
            <h2 id="settings-title">讓這段對話更合用</h2>
          </div>
          <button
            type="button"
            class="icon-button"
            aria-label="關閉對話設定"
            [disabled]="busy()"
            (click)="dialog.close()"
          >
            <nx-icon name="close" />
          </button>
        </div>
        <label for="conversation-instruction">對話指令</label
        ><textarea
          nxField
          id="conversation-instruction"
          [formField]="fields.instruction"
          rows="5"
          placeholder="例如：先提供摘要，再列出待辦事項；專有名詞保留英文。"
        ></textarea>
        <p class="panel-note">套用至這段對話後續的提問；已生成的回答與其他對話保留原樣。</p>
        <label for="conversation-labels">標籤</label
        ><input
          nxField
          id="conversation-labels"
          [formField]="fields.labels"
          placeholder="工作, 企劃, 會議"
        />
        <p class="panel-note">以逗號分隔，最多 5 個標籤，每個最多 24 字元。</p>
        @if (session.has('projects') || projectId()) {
          <span class="field-label" aria-hidden="true">所屬專案</span
          ><nx-select
            label="對話所屬專案"
            [value]="projectId()"
            [options]="projectOptions()"
            [disabled]="busy()"
            (valueChange)="assignProject($event)"
          />
          <p class="panel-note">變更後立即儲存。加入專案後，後續提問會使用共用指示與參考文件。</p>
        }
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <div class="dialog-actions">
          <button
            type="button"
            class="secondary-button"
            [disabled]="busy()"
            (click)="dialog.close()"
          >
            取消</button
          ><button type="submit" class="primary-button" [disabled]="busy() || fields().invalid()">
            {{ busy() ? '儲存中…' : '儲存設定' }}
          </button>
        </div>
      </form>
    </div>
  </dialog>`,
})
export class ConversationSettingsDialog {
  readonly session = inject(WorkspaceSession);
  private readonly projects = inject(ProjectsApi);
  private readonly conversations = inject(ChatConversations);
  private readonly scope = inject(ViewScope);
  readonly projectId = signal('');
  /** The conversation being edited; each opening reads the projects it can move to. */
  private readonly target = signal<ConversationDto | null>(null);
  private readonly projectsRead = apiResource({
    params: () => (this.target() && this.session.has('projects') ? this.target() : undefined),
    loader: () => this.projects.list(),
  });
  readonly projectOptions = computed(() => [
    { value: '', label: '個人對話' },
    ...(this.projectsRead.value() ?? [])
      .filter((x) => !x.isArchived || x.resource.id === this.target()?.projectId)
      .map((x) => ({ value: x.resource.id, label: x.resource.name })),
  ]);
  readonly save =
    input.required<
      (conversation: ConversationDto, settings: ConversationSettingsRequest) => Promise<boolean>
    >();
  readonly model = signal({ instruction: '', labels: '' });
  readonly fields = form(this.model, (schema) => {
    maxLength(schema.instruction, 4000);
    maxLength(schema.labels, 150);
  });
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.projectsRead.error());
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  open(conversation: ConversationDto) {
    this.target.set(conversation);
    this.actionError.set('');
    this.projectId.set(conversation.projectId ?? '');
    this.model.set({
      instruction: conversation.systemInstruction,
      labels: (conversation.labels ?? []).join(', '),
    });
    this.dialog()?.nativeElement.showModal();
  }
  async assignProject(value: string) {
    const target = this.target();
    if (!target || this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      if (await this.conversations.assignProject(target, value || null)) {
        if (valid()) this.projectId.set(value);
      } else if (valid())
        this.actionError.set('未能變更專案，請確認沒有正在生成的回答，並檢查專案權限。');
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async submit(event: Event) {
    event.preventDefault();
    const target = this.target();
    if (this.busy() || !target || this.fields().invalid()) return;
    const labels = this.model()
      .labels.split(/[,，]/)
      .map((label) => label.trim())
      .filter(Boolean);
    if (labels.length > 5 || labels.some((label) => label.length > 24)) {
      this.actionError.set('最多 5 個標籤，每個最多 24 字元。');
      return;
    }
    this.busy.set(true);
    this.actionError.set('');
    try {
      if (await this.save()(target, { systemInstruction: this.model().instruction, labels }))
        this.dialog()?.nativeElement.close();
      else this.actionError.set('未能儲存設定，請確認連線後重試。');
    } finally {
      this.busy.set(false);
    }
  }
}
