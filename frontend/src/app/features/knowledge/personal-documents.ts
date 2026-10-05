import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiTransport } from '../../core/api/api-transport';
import type { DocumentInfo } from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { Icon } from '../../shared/ui/icon';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { SearchField } from '../../shared/ui/search-field';

@Component({
  selector: 'nx-personal-documents',
  imports: [RouterLink, Icon, SearchField, ConfirmDialog],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button class="secondary-button" type="button" (click)="open()">
      <nx-icon name="document" />個人文件
    </button>
    <dialog #dialog class="platform-dialog" aria-label="個人文件">
      <div class="dialog-scroll">
        <div class="dialog-heading">
          <h2>個人文件</h2>
          <button class="icon-button" aria-label="關閉個人文件" (click)="dialog.close()">
            <nx-icon name="close" />
          </button>
        </div>
        <p class="form-note">
          獨立閱讀的文件，以及刪除專案後保留的文件。此處僅顯示自己的最近 200 份文件。
        </p>
        <nx-search-field
          label="搜尋個人文件"
          placeholder="尋找文件"
          [value]="filter()"
          (valueChange)="filter.set($event)"
        />
        @if (error()) {
          <p class="error-banner" role="alert">{{ error() }}</p>
        }
        @if (loading()) {
          <p role="status">正在載入個人文件…</p>
        } @else {
          <div class="personal-document-list">
            @for (file of visible(); track file.id) {
              <div class="personal-document-row">
                <a [routerLink]="['/reader', file.id]" (click)="dialog.close()"
                  ><nx-icon name="document" /><span>{{ file.fileName }}</span></a
                >
                <button
                  class="icon-button danger-text"
                  [attr.aria-label]="'刪除 ' + file.fileName"
                  [disabled]="busy()"
                  (click)="remove(file)"
                >
                  <nx-icon name="trash" />
                </button>
              </div>
            } @empty {
              <p class="form-note">沒有符合的個人文件。</p>
            }
          </div>
        }
      </div>
    </dialog>
    <nx-confirm-dialog />`,
  styles: `
    .personal-document-list {
      display: grid;
      gap: 6px;
      margin-top: 16px;
      max-height: 48dvh;
      overflow: auto;
    }
    .personal-document-row {
      display: flex;
      align-items: center;
      gap: 12px;
      border-bottom: 1px solid var(--line);
      padding: 8px 0;
    }
    .personal-document-row a {
      display: flex;
      align-items: center;
      gap: 10px;
      flex: 1;
      min-width: 0;
    }
    .personal-document-row a span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class PersonalDocuments {
  private readonly api = inject(ApiTransport);
  private readonly scope = inject(ViewScope);
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly confirm = viewChild.required(ConfirmDialog);
  readonly files = signal<DocumentInfo[]>([]);
  readonly filter = signal('');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly visible = computed(() =>
    this.files().filter((x) => x.fileName.toLowerCase().includes(this.filter().toLowerCase())),
  );
  async open() {
    this.dialog().nativeElement.showModal();
    this.loading.set(true);
    this.error.set('');
    const valid = this.scope.guard();
    try {
      const files = await this.api.json<DocumentInfo[]>('/documents');
      if (valid()) this.files.set(files);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  async remove(file: DocumentInfo) {
    const valid = this.scope.guard();
    if (
      this.busy() ||
      !(await this.confirm().ask({
        title: `刪除「${file.fileName}」？`,
        message: '將移除此閱讀文件。仍被對話或其他資源引用的原始附件會保留。',
        confirm: '刪除文件',
        danger: true,
      })) ||
      !valid()
    )
      return;
    this.busy.set(true);
    try {
      await this.api.json<void>(`/documents/${file.id}`, 'DELETE');
      if (valid()) this.files.update((x) => x.filter((y) => y.id !== file.id));
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
