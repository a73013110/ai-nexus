import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { DocumentInfo } from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { KnowledgeApi } from './knowledge-api';

@Component({
  selector: 'nx-text-source-editor',
  imports: [Notice, CompactDialog, Field, ConfirmDialog],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #dialog
      class="workspace-dialog text-source-dialog"
      aria-label="純文字來源編輯器"
      (cancel)="cancel($event)"
    >
      <form class="platform-form dialog-scroll" (submit)="save($event)">
        <h2>{{ documentId() ? '編輯純文字來源' : '加入純文字來源' }}</h2>
        <p class="form-note">貼上筆記或文字內容。儲存後會在背景建立索引，完成即可供對話查詢。</p>
        @if (loading()) {
          <p role="status">正在讀取內容…</p>
        }
        <label
          >來源名稱<input
            nxField
            required
            maxlength="120"
            [readOnly]="busy() || loading()"
            [value]="title()"
            (input)="title.set($any($event.target).value)"
        /></label>
        <label
          >純文字內容<textarea
            nxField
            required
            rows="15"
            maxlength="64000"
            [readOnly]="busy() || loading()"
            [value]="text()"
            placeholder="將文字貼在這裡…"
            (input)="text.set($any($event.target).value)"
          ></textarea>
        </label>
        <p class="form-note">
          {{ text().length.toLocaleString() }} / 64,000 字元 · 原始內容會保留為文字檔
        </p>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <div class="dialog-actions">
          <button type="button" class="secondary-button" [disabled]="busy()" (click)="close()">
            取消
          </button>
          <button
            class="primary-button"
            [disabled]="busy() || loading() || !title().trim() || !text().trim()"
          >
            {{ busy() ? '正在儲存…' : '儲存並建立索引' }}
          </button>
        </div>
      </form>
    </dialog>
    <nx-confirm-dialog />`,
  styles: `
    .text-source-dialog {
      width: min(780px, calc(100vw - 32px));
    }
    textarea {
      resize: vertical;
      min-height: 240px;
      font-family: var(--font-code);
      font-size: var(--text-body);
      line-height: var(--line-reading);
    }
  `,
})
export class TextSourceEditor {
  private readonly api = inject(KnowledgeApi);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly confirm = viewChild.required(ConfirmDialog);
  readonly saved = output<DocumentInfo>();
  readonly title = signal('');
  readonly text = signal('');
  readonly documentId = signal('');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  private collection = '';
  private version = 0;
  private initial = '';
  private sequence = 0;
  async open(collection: string, document?: DocumentInfo) {
    const sequence = ++this.sequence,
      valid = this.scope.guard();
    this.collection = collection;
    this.documentId.set(document?.id || '');
    this.title.set('');
    this.text.set('');
    this.error.set('');
    this.initial = '';
    this.loading.set(!!document);
    this.dialog().nativeElement.showModal();
    try {
      if (document) {
        const source = await this.api.text(document.id);
        if (!valid() || sequence !== this.sequence) return;
        this.title.set(source.title);
        this.text.set(source.text);
        this.version = source.version;
      }
      this.initial = this.signature();
    } catch (e) {
      if (valid() && sequence === this.sequence) this.error.set(this.scope.message(e));
    } finally {
      if (valid() && sequence === this.sequence) this.loading.set(false);
    }
  }
  private signature() {
    return JSON.stringify([this.title(), this.text()]);
  }
  cancel(event: Event) {
    event.preventDefault();
    if (!this.busy()) void this.close();
  }
  async close() {
    if (this.busy()) return;
    if (
      this.signature() !== this.initial &&
      (this.title() || this.text()) &&
      !(await this.confirm().ask({
        title: '放棄未儲存的內容',
        message: '這次文字修改尚未儲存。',
        confirm: '放棄修改',
      }))
    )
      return;
    ++this.sequence;
    this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.busy() || this.loading() || !this.title().trim() || !this.text().trim()) return;
    const valid = this.scope.guard(),
      sequence = this.sequence;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = this.documentId()
        ? await this.api.updateText(this.documentId(), this.title(), this.text(), this.version)
        : await this.api.createText(this.collection, this.title(), this.text());
      if (valid() && sequence === this.sequence) {
        this.dialog().nativeElement.close();
        this.saved.emit(result);
      }
    } catch (e) {
      if (valid() && sequence === this.sequence) this.error.set(this.scope.message(e));
    } finally {
      if (valid() && sequence === this.sequence) this.busy.set(false);
    }
  }
}
