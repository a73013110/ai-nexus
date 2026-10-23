import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
  computed,
} from '@angular/core';
import { FormField, form, maxLength, readonly, required } from '@angular/forms/signals';
import type { PromptTemplateDto } from '@core/api/schema';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Icon } from '@shared/ui/icon';
import { PendingButton } from '@shared/ui/pending-button';
import { ViewScope } from '@shared/browser/view-scope';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { PromptsApi } from './prompts-api';
import { promptLibraryTour } from './prompt-library-tour';

@Component({
  selector: 'nx-prompt-library',
  imports: [Notice, CompactDialog, DialogHeader, Field, Icon, FormField, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ViewScope],
  styleUrl: './prompt-library-dialog.scss',
  template: `<dialog
    nxCompactDialog
    size="lg"
    #dialog
    class="platform-dialog library-dialog"
    [busy]="busy()"
  >
    <nx-dialog-header heading="常用提示詞" closeLabel="關閉範本庫" [tour]="tour()" />
    <div class="dialog-body dialog-body-flush library-layout">
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      <div
        class="template-list"
        [class.is-refreshing]="listRefreshing()"
        [attr.aria-busy]="listRefreshing() || null"
      >
        <button type="button" class="template-create" (click)="compose(null)">
          <nx-icon name="plus" />新增範本
        </button>
        @if (firstLoad()) {
          <div class="template-skeleton" role="status">
            @for (width of skeleton; track $index) {
              <span class="skeleton" [style.width.%]="width"></span>
            }
            <span class="sr-only">正在載入範本…</span>
          </div>
        }
        @for (prompt of prompts(); track prompt.id) {
          <div class="template-row" [class.is-editing]="editingId() === prompt.id">
            <button type="button" class="template-use" (click)="choose(prompt)">
              <strong>{{ prompt.title }}</strong
              ><small>{{ prompt.content.slice(0, 64) }}</small></button
            ><button
              type="button"
              class="icon-button template-edit"
              [attr.aria-label]="'編輯範本：' + prompt.title"
              (click)="compose(prompt)"
            >
              <nx-icon name="edit" /></button
            ><button
              nxPending
              type="button"
              class="icon-button template-delete"
              [pending]="deleting().has(prompt.id)"
              [attr.aria-label]="'刪除範本：' + prompt.title"
              (click)="remove(prompt.id)"
            >
              <nx-icon name="trash" />
            </button>
          </div>
        } @empty {
          @if (!firstLoad()) {
            <p class="template-note">還沒有範本。可將目前草稿保存為第一個範本。</p>
          }
        }
      </div>
      <form class="template-editor" (submit)="save($event)">
        <h3>{{ editingId() ? '編輯範本' : '新增範本' }}</h3>
        <label
          >範本名稱<input
            #titleField
            nxField
            [formField]="fields.title"
            placeholder="例如：會議摘要"
        /></label>
        <label
          >提示詞內容<textarea
            nxField
            [formField]="fields.content"
            placeholder="說明任務、格式與預期結果…"
            rows="7"
          ></textarea>
        </label>
        <div class="template-actions">
          <button
            nxPending
            type="submit"
            class="primary-button"
            [pending]="busy()"
            [disabled]="fields().invalid()"
          >
            {{ busy() ? '儲存中…' : '儲存範本' }}
          </button>
        </div>
      </form>
    </div>
  </dialog>`,
})
export class PromptLibraryDialog {
  private readonly api = inject(PromptsApi);
  private readonly scope = inject(ViewScope);
  private readonly session = inject(WorkspaceSession);
  readonly draft = input('');
  readonly used = output<string>();
  /** 每次開啟都會讀取已儲存的提示。 */
  private readonly opened = signal(0);
  private readonly promptsRead = apiResource({
    params: () => this.opened() || undefined,
    loader: () => this.api.prompts(),
  });
  readonly prompts = computed<PromptTemplateDto[]>(() => this.promptsRead.value() ?? []);
  /** 每次開啟都會重讀；還沒有範本時畫骨架，已有範本時保留舊列並淡化。 */
  readonly firstLoad = computed(() => this.promptsRead.refreshing() && !this.prompts().length);
  readonly listRefreshing = computed(
    () => this.promptsRead.refreshing() && this.prompts().length > 0,
  );
  /** 骨架列的寬度（%）。 */
  protected readonly skeleton = [82, 64, 74];
  readonly busy = signal(false);
  /** 刪除中的範本：只擋那一列的刪除鈕，其他列照常可用。 */
  readonly deleting = signal<ReadonlySet<string>>(new Set());
  private readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.promptsRead.error());
  readonly editingId = signal<string | null>(null);
  readonly tour = computed(() =>
    promptLibraryTour({ editing: this.editingId() !== null, testing: this.session.testing() }),
  );
  private readonly model = signal({ title: '', content: '' });
  readonly fields = form(this.model, (schema) => {
    required(schema.title);
    maxLength(schema.title, 80);
    required(schema.content);
    maxLength(schema.content, 12000);
    // 儲存中欄位唯讀（不停用，焦點留在原處）。
    readonly(schema.title, () => this.busy());
    readonly(schema.content, () => this.busy());
  });
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly titleField = viewChild<ElementRef<HTMLInputElement>>('titleField');
  open() {
    this.actionError.set('');
    this.edit(null);
    this.dialog()?.nativeElement.showModal();
    this.opened.update((value) => value + 1);
  }
  private edit(prompt: PromptTemplateDto | null) {
    this.editingId.set(prompt?.id ?? null);
    this.model.set({ title: prompt?.title ?? '', content: prompt?.content ?? this.draft() });
  }
  /** 焦點移入時，若編輯器在列表下方堆疊，會把它捲到可見處。 */
  compose(prompt: PromptTemplateDto | null) {
    this.edit(prompt);
    this.titleField()?.nativeElement.focus();
  }
  choose(prompt: PromptTemplateDto) {
    this.dialog()?.nativeElement.close();
    this.used.emit(prompt.content);
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.busy() || this.fields().invalid()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.api.savePrompt(this.editingId(), this.model().title, this.model().content);
      if (!valid()) return;
      this.promptsRead.reload();
      this.edit(null);
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  async remove(id: string) {
    if (this.deleting().has(id)) return;
    const valid = this.scope.guard();
    this.deleting.update((ids) => new Set(ids).add(id));
    try {
      await this.api.deletePrompt(id);
      if (!valid()) return;
      this.promptsRead.value.update((prompts) => prompts?.filter((prompt) => prompt.id !== id));
      if (this.editingId() === id) this.edit(null);
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      this.deleting.update((ids) => {
        const next = new Set(ids);
        next.delete(id);
        return next;
      });
    }
  }
}
