import { apiResource } from '../../core/api/api-resource';
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
  linkedSignal,
  computed,
} from '@angular/core';
import type { DocumentDto } from '../../core/api/schema';
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
  readonly saved = output<DocumentDto>();
  /** What the dialog edits: a new source in a collection, or an existing document's text. */
  private readonly editing = signal<{ collection: string; documentId: string } | null>(null);
  private readonly sourceRead = apiResource({
    params: () => {
      const editing = this.editing();
      return editing?.documentId ? editing : undefined;
    },
    loader: async (editing) => ({ editing, source: await this.api.text(editing.documentId) }),
  });
  private readonly source = computed(() => {
    const value = this.sourceRead.value();
    return value && value.editing === this.editing() ? value.source : null;
  });
  readonly documentId = computed(() => this.editing()?.documentId ?? '');
  readonly title = linkedSignal(() => this.source()?.title ?? '');
  readonly text = linkedSignal(() => this.source()?.text ?? '');
  /** The content as opened, to tell whether closing would discard edits. */
  private readonly initial = computed(() =>
    JSON.stringify([this.source()?.title ?? '', this.source()?.text ?? '']),
  );
  readonly loading = this.sourceRead.loading;
  readonly busy = signal(false);
  readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.sourceRead.error());
  open(collection: string, document?: DocumentDto) {
    this.saveError.set('');
    this.editing.set({ collection, documentId: document?.id || '' });
    this.dialog().nativeElement.showModal();
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
      this.signature() !== this.initial() &&
      (this.title() || this.text()) &&
      !(await this.confirm().ask({
        title: '放棄未儲存的內容',
        message: '這次文字修改尚未儲存。',
        confirm: '放棄修改',
      }))
    )
      return;
    this.editing.set(null);
    this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.busy() || this.loading() || !this.title().trim() || !this.text().trim()) return;
    const valid = this.scope.guard(),
      editing = this.editing(),
      source = this.source(),
      current = () => valid() && editing === this.editing();
    if (!editing) return;
    this.busy.set(true);
    this.saveError.set('');
    try {
      const result = editing.documentId
        ? await this.api.updateText(editing.documentId, this.title(), this.text(), source!.version)
        : await this.api.createText(editing.collection, this.title(), this.text());
      if (current()) {
        this.dialog().nativeElement.close();
        this.saved.emit(result);
      }
    } catch (e) {
      if (current()) this.saveError.set(this.scope.message(e));
    } finally {
      if (current()) this.busy.set(false);
    }
  }
}
