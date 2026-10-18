import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
import { safeMessage } from '@core/errors/safe-errors';
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
import { FormField, form, maxLength, required } from '@angular/forms/signals';
import type { PromptTemplateDto } from '@core/api/schema';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Icon } from '@shared/ui/icon';
import { PromptsApi } from './prompts-api';

@Component({
  selector: 'nx-prompt-library',
  imports: [Notice, CompactDialog, DialogHeader, Field, Icon, FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './prompt-library-dialog.scss',
  template: `<dialog nxCompactDialog size="lg" #dialog class="platform-dialog library-dialog">
    <nx-dialog-header heading="常用提示詞" closeLabel="關閉範本庫" />
    <div class="dialog-body dialog-body-flush library-layout">
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      <div class="template-list">
        <button type="button" class="template-create" (click)="compose(null)">
          <nx-icon name="plus" />新增範本
        </button>
        @if (loading()) {
          <p class="template-note" role="status">正在取得範本…</p>
        }
        @for (prompt of prompts(); track prompt.id) {
          <div class="template-row" [class.is-editing]="editingId() === prompt.id">
            <button type="button" class="template-use" (click)="choose(prompt)">
              <strong>{{ prompt.title }}</strong
              ><small>{{ prompt.content.slice(0, 64) }}</small></button
            ><button
              type="button"
              class="icon-button"
              [attr.aria-label]="'編輯範本：' + prompt.title"
              (click)="compose(prompt)"
            >
              <nx-icon name="edit" /></button
            ><button
              type="button"
              class="icon-button"
              [disabled]="busy()"
              [attr.aria-label]="'刪除範本：' + prompt.title"
              (click)="remove(prompt.id)"
            >
              <nx-icon name="trash" />
            </button>
          </div>
        } @empty {
          @if (!loading()) {
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
          <button type="submit" class="primary-button" [disabled]="busy() || fields().invalid()">
            {{ busy() ? '儲存中…' : '儲存範本' }}
          </button>
        </div>
      </form>
    </div>
  </dialog>`,
})
export class PromptLibraryDialog {
  private readonly api = inject(PromptsApi);
  readonly draft = input('');
  readonly used = output<string>();
  /** 每次開啟都會讀取已儲存的提示。 */
  private readonly opened = signal(0);
  private readonly promptsRead = apiResource({
    params: () => this.opened() || undefined,
    loader: () => this.api.prompts(),
  });
  readonly prompts = computed<PromptTemplateDto[]>(() => this.promptsRead.value() ?? []);
  readonly loading = this.promptsRead.loading;
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.promptsRead.error());
  readonly editingId = signal<string | null>(null);
  readonly model = signal({ title: '', content: '' });
  readonly fields = form(this.model, (schema) => {
    required(schema.title);
    maxLength(schema.title, 80);
    required(schema.content);
    maxLength(schema.content, 12000);
  });
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly titleField = viewChild<ElementRef<HTMLInputElement>>('titleField');
  open() {
    this.actionError.set('');
    this.edit(null);
    this.dialog()?.nativeElement.showModal();
    this.opened.update((value) => value + 1);
  }
  edit(prompt: PromptTemplateDto | null) {
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
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.api.savePrompt(this.editingId(), this.model().title, this.model().content);
      this.promptsRead.reload();
      this.edit(null);
    } catch (error) {
      this.report(error);
    } finally {
      this.busy.set(false);
    }
  }
  async remove(id: string) {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await this.api.deletePrompt(id);
      this.promptsRead.value.update((prompts) => prompts?.filter((prompt) => prompt.id !== id));
      if (this.editingId() === id) this.edit(null);
    } catch (error) {
      this.report(error);
    } finally {
      this.busy.set(false);
    }
  }
  private report(error: unknown) {
    this.actionError.set(safeMessage(error));
  }
}
