import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import { safeMessage } from '../../core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength, required } from '@angular/forms/signals';
import type { PromptTemplateDto } from '../../core/api/schema';
import { Icon } from '../../shared/ui/icon';
import { WorkspaceApi } from './workspace-api';

@Component({
  selector: 'nx-prompt-library',
  imports: [Notice, CompactDialog, Field, Icon, FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <dialog
    nxCompactDialog
    #dialog
    class="workspace-dialog library-dialog"
    aria-labelledby="library-title"
  >
    <div class="dialog-scroll">
      <div class="dialog-heading">
        <div>
          <span class="panel-eyebrow">PERSONAL LIBRARY</span>
          <h2 id="library-title">常用提示詞</h2>
        </div>
        <button type="button" class="icon-button" aria-label="關閉範本庫" (click)="dialog.close()">
          <nx-icon name="close" />
        </button>
      </div>
      <p class="panel-note">把常做的工作保存成範本，下次一鍵帶入提問。</p>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      <div class="library-layout">
        <div class="template-list">
          <button type="button" class="template-create" (click)="edit(null)">
            <nx-icon name="plus" />新增範本
          </button>
          @if (loading()) {
            <p class="panel-note" role="status">正在取得範本…</p>
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
                (click)="edit(prompt)"
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
              <p class="panel-note">還沒有範本。可將目前草稿保存為第一個範本。</p>
            }
          }
        </div>
        <form class="template-editor" (submit)="save($event)">
          <h3>{{ editingId() ? '編輯範本' : '新增範本' }}</h3>
          <label for="prompt-title">範本名稱</label
          ><input
            nxField
            id="prompt-title"
            [formField]="fields.title"
            placeholder="例如：會議摘要"
          /><label for="prompt-content">提示詞內容</label
          ><textarea
            nxField
            id="prompt-content"
            [formField]="fields.content"
            placeholder="說明任務、格式與預期結果…"
            rows="7"
          ></textarea>
          <div class="dialog-actions">
            <button type="submit" class="primary-button" [disabled]="busy() || fields().invalid()">
              {{ busy() ? '儲存中…' : '儲存範本' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </dialog>`,
})
export class PromptLibraryDialog {
  private readonly api = inject(WorkspaceApi);
  readonly draft = input('');
  readonly used = output<string>();
  readonly prompts = signal<PromptTemplateDto[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly editingId = signal<string | null>(null);
  readonly model = signal({ title: '', content: '' });
  readonly fields = form(this.model, (schema) => {
    required(schema.title);
    maxLength(schema.title, 80);
    required(schema.content);
    maxLength(schema.content, 12000);
  });
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  async open() {
    this.error.set('');
    this.edit(null);
    this.dialog()?.nativeElement.showModal();
    this.loading.set(true);
    try {
      this.prompts.set(await this.api.prompts());
    } catch (error) {
      this.report(error);
    } finally {
      this.loading.set(false);
    }
  }
  edit(prompt: PromptTemplateDto | null) {
    this.editingId.set(prompt?.id ?? null);
    this.model.set({ title: prompt?.title ?? '', content: prompt?.content ?? this.draft() });
  }
  choose(prompt: PromptTemplateDto) {
    this.dialog()?.nativeElement.close();
    this.used.emit(prompt.content);
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.busy() || this.fields().invalid()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.savePrompt(this.editingId(), this.model().title, this.model().content);
      this.prompts.set(await this.api.prompts());
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
      this.prompts.update((prompts) => prompts.filter((prompt) => prompt.id !== id));
      if (this.editingId() === id) this.edit(null);
    } catch (error) {
      this.report(error);
    } finally {
      this.busy.set(false);
    }
  }
  private report(error: unknown) {
    this.error.set(safeMessage(error));
  }
}
