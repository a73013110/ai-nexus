import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
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
import type { ConversationDto, ConversationSettingsRequest } from '@core/api/schema';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Select } from '@shared/ui/select';
import { ProjectsApi } from '@features/work/projects/projects-api';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';

// 與後端 invalid_labels 的限制一致；送出前先擋，說明與錯誤訊息共用同一組數字。
const MAX_LABELS = 5;
const MAX_LABEL_LENGTH = 24;
const LABEL_RULE = `最多 ${MAX_LABELS} 個標籤，每個最多 ${MAX_LABEL_LENGTH} 字元。`;

@Component({
  selector: 'nx-conversation-settings',
  imports: [Notice, CompactDialog, DialogHeader, Field, FormField, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog"
    (cancel)="busy() ? $event.preventDefault() : null"
  >
    <form class="dialog-form" (submit)="submit($event)">
      <nx-dialog-header heading="讓這段對話更合用" closeLabel="關閉對話設定" />
      <div class="dialog-body">
        <label
          >對話指令<textarea
            nxField
            [formField]="fields.instruction"
            rows="5"
            placeholder="例如：先提供摘要，再列出待辦事項；專有名詞保留英文。"
          ></textarea>
        </label>
        <p class="panel-note">套用至這段對話後續的提問；已生成的回答與其他對話保留原樣。</p>
        <label
          >標籤<input nxField [formField]="fields.labels" placeholder="工作, 企劃, 會議"
        /></label>
        <p class="panel-note">以逗號分隔，{{ labelRule }}</p>
        @if (session.has('projects') || projectId()) {
          <span class="field-label" aria-hidden="true">所屬專案</span
          ><nx-select
            label="對話所屬專案"
            [value]="projectId()"
            [options]="projectOptions()"
            [disabled]="busy()"
            (valueChange)="chooseProject($event)"
          />
          <p class="panel-note">變更後立即儲存。加入專案後，後續提問會使用共用指示與參考文件。</p>
        }
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
      </div>
      <footer class="dialog-foot">
        <button type="button" class="secondary-button" [disabled]="busy()" (click)="dialog.close()">
          取消
        </button>
        <button type="submit" class="primary-button" [disabled]="busy() || fields().invalid()">
          {{ busy() ? '儲存中…' : '儲存設定' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class ConversationSettingsDialog {
  readonly session = inject(WorkspaceSession);
  private readonly projects = inject(ProjectsApi);
  private readonly scope = inject(ViewScope);
  readonly projectId = signal('');
  /** 正在編輯的對話。 */
  private readonly target = signal<ConversationDto | null>(null);
  /** 每次開啟都會讀取可移入的專案，即使是同一個對話。 */
  private readonly opened = signal(0);
  private readonly projectsRead = apiResource({
    params: () => (this.opened() && this.session.has('projects') ? this.opened() : undefined),
    loader: () => this.projects.list(),
  });
  readonly projectOptions = computed(() => [
    { value: '', label: '個人對話' },
    ...(this.projectsRead.value() ?? [])
      .filter((item) => !item.isArchived || item.resource.id === this.target()?.projectId)
      .map((item) => ({ value: item.resource.id, label: item.resource.name })),
  ]);
  readonly save =
    input.required<
      (conversation: ConversationDto, settings: ConversationSettingsRequest) => Promise<boolean>
    >();
  /** 把對話移入專案，或以 `null` 移回個人。 */
  readonly assignProject =
    input.required<(conversation: ConversationDto, projectId: string | null) => Promise<boolean>>();
  readonly model = signal({ instruction: '', labels: '' });
  readonly fields = form(this.model, (schema) => {
    maxLength(schema.instruction, 4000);
    maxLength(schema.labels, 150);
  });
  readonly labelRule = LABEL_RULE;
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.projectsRead.error());
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  open(conversation: ConversationDto) {
    this.target.set(conversation);
    this.opened.update((value) => value + 1);
    this.actionError.set('');
    this.projectId.set(conversation.projectId ?? '');
    this.model.set({
      instruction: conversation.systemInstruction,
      labels: (conversation.labels ?? []).join(', '),
    });
    this.dialog()?.nativeElement.showModal();
  }
  async chooseProject(value: string) {
    const target = this.target();
    if (!target || this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      if (await this.assignProject()(target, value || null)) {
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
    if (labels.length > MAX_LABELS || labels.some((label) => label.length > MAX_LABEL_LENGTH)) {
      this.actionError.set(LABEL_RULE);
      return;
    }
    const valid = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      const saved = await this.save()(target, {
        systemInstruction: this.model().instruction,
        labels,
      });
      if (!valid()) return;
      if (saved) this.dialog()?.nativeElement.close();
      else this.actionError.set('未能儲存設定，請確認連線後重試。');
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
