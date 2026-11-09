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
import { FormField, form, maxLength, readonly } from '@angular/forms/signals';
import type { ConversationDto, ConversationSettingsRequest } from '@core/api/schema';
import { DialogHeader } from '@shared/ui/dialog-header';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { ProjectsApi } from '@features/work/projects/projects-api';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { uniqueId } from '@shared/browser/unique-id';

// 與後端 invalid_labels 的限制一致；送出前先擋，說明與錯誤訊息共用同一組數字。
const MAX_LABELS = 5;
const MAX_LABEL_LENGTH = 24;
const LABEL_RULE = `最多 ${MAX_LABELS} 個標籤，每個最多 ${MAX_LABEL_LENGTH} 字元。`;

@Component({
  selector: 'nx-conversation-settings',
  imports: [Notice, CompactDialog, DialogHeader, Field, FormField, PendingButton, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog nxCompactDialog #dialog class="platform-dialog" [busy]="saving.pending()">
    <form class="dialog-form" (submit)="submit($event)">
      <nx-dialog-header heading="讓這段對話更合用" closeLabel="關閉對話設定" />
      <div class="dialog-body">
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <div class="form-sections">
          <section class="form-section" role="group" [attr.aria-labelledby]="id + '-answer'">
            <header class="form-section-head">
              <h3 [id]="id + '-answer'">回答方式</h3>
              <p class="form-note">套用至這段對話之後的提問；已生成的回答與其他對話保留原樣。</p>
            </header>
            <div class="form-section-body">
              <label
                >對話指令<textarea
                  nxField
                  [formField]="fields.instruction"
                  rows="5"
                  placeholder="例如：先提供摘要，再列出待辦事項；專有名詞保留英文。"
                ></textarea>
              </label>
            </div>
          </section>
          <section class="form-section" role="group" [attr.aria-labelledby]="id + '-organize'">
            <header class="form-section-head">
              <h3 [id]="id + '-organize'">整理</h3>
            </header>
            <div class="form-section-body">
              <label
                >標籤<input
                  nxField
                  [formField]="fields.labels"
                  placeholder="工作, 企劃, 會議"
                  [attr.aria-describedby]="id + '-labels-note'"
              /></label>
              <p class="form-note" [id]="id + '-labels-note'">以逗號分隔，{{ labelRule }}</p>
              @if (session.has('projects') || projectId()) {
                <label class="form-field-short"
                  >所屬專案<nx-select
                    label="對話所屬專案"
                    [value]="projectId()"
                    [options]="projectOptions()"
                    [loading]="projectsRead.refreshing() || moving()"
                    [disabled]="saving.pending() && !moving()"
                    (valueChange)="chooseProject($event)"
                /></label>
                <p class="form-note">
                  選擇後立即儲存；加入專案後，之後的提問會使用專案的共用指示與參考文件。
                </p>
                @if (moving()) {
                  <span class="sr-only" role="status">正在儲存所屬專案…</span>
                }
              }
            </div>
          </section>
        </div>
      </div>
      <footer class="dialog-foot">
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="saving.pending() || null"
          (click)="saving.pending() || dialog.close()"
        >
          取消
        </button>
        <button
          nxPending
          type="submit"
          class="primary-button"
          [pending]="saving.pending() && !moving()"
          [disabled]="fields().invalid()"
        >
          {{ saving.pending() && !moving() ? '儲存中…' : '儲存設定' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class ConversationSettingsDialog {
  readonly id = uniqueId('nx-conversation-settings');
  readonly session = inject(WorkspaceSession);
  private readonly projects = inject(ProjectsApi);
  readonly projectId = signal('');
  /** 正在編輯的對話。 */
  private readonly target = signal<ConversationDto | null>(null);
  /** 每次開啟都會讀取可移入的專案，即使是同一個對話。 */
  private readonly opened = signal(0);
  protected readonly projectsRead = apiResource({
    params: () => (this.opened() && this.session.has('projects') ? this.opened() : undefined),
    loader: () => this.projects.list(),
  });
  readonly projectOptions = computed(() => [
    { value: '', label: '個人對話' },
    ...(this.projectsRead.value()?.items ?? [])
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
    // 送出中欄位唯讀（不停用，焦點留在原處）；失敗後解除即可修改再送出。
    readonly(schema.instruction, () => this.saving.pending());
    readonly(schema.labels, () => this.saving.pending());
  });
  readonly labelRule = LABEL_RULE;
  /** 儲存設定（`settings`）或立即變更所屬專案（`project`）；一次一件，其他欄位等它完成。 */
  readonly saving = action();
  /** 所屬專案立即儲存中：下拉鈕轉圈。 */
  readonly moving = computed(() => this.saving.pendingFor('project'));
  readonly error = computed(() => this.saving.error() || this.projectsRead.error());
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  open(conversation: ConversationDto) {
    this.target.set(conversation);
    this.opened.update((value) => value + 1);
    this.saving.error.set('');
    this.projectId.set(conversation.projectId ?? '');
    this.model.set({
      instruction: conversation.systemInstruction,
      labels: (conversation.labels ?? []).join(', '),
    });
    this.dialog()?.nativeElement.showModal();
  }
  async chooseProject(value: string) {
    const target = this.target();
    if (!target) return;
    const outcome = await this.saving.run(() => this.assignProject()(target, value || null), {
      key: 'project',
    });
    if (!outcome.ok) return;
    if (outcome.value) this.projectId.set(value);
    else this.saving.fail('未能變更專案，請確認沒有正在生成的回答，並檢查專案權限。');
  }
  async submit(event: Event) {
    event.preventDefault();
    const target = this.target();
    if (this.saving.pending() || !target || this.fields().invalid()) return;
    const labels = this.model()
      .labels.split(/[,，]/)
      .map((label) => label.trim())
      .filter(Boolean);
    if (labels.length > MAX_LABELS || labels.some((label) => label.length > MAX_LABEL_LENGTH)) {
      this.saving.fail(LABEL_RULE);
      return;
    }
    const outcome = await this.saving.run(
      () => this.save()(target, { systemInstruction: this.model().instruction, labels }),
      { key: 'settings' },
    );
    if (!outcome.ok) return;
    if (outcome.value) this.dialog()?.nativeElement.close();
    else this.saving.fail('未能儲存設定，請確認連線後重試。');
  }
}
