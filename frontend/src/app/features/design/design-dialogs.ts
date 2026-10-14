import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { DialogHeader } from '../../shared/ui/dialog-header';
import { Field } from '../../shared/ui/field';
import { Icon } from '../../shared/ui/icon';
import { SearchField } from '../../shared/ui/search-field';
import { ToastService } from '../../shared/ui/toast-service';
import { DesignExample, DesignSection } from './design-section';

const FILES = [
  { name: '季度營運報告.pdf', detail: '2.4 MB · 10/08' },
  { name: '產品路線圖.xlsx', detail: '860 KB · 10/06' },
  { name: '客服常見問答.docx', detail: '412 KB · 10/03' },
  { name: '設計規範.md', detail: '38 KB · 09/29' },
  { name: '會議紀錄 0925.txt', detail: '12 KB · 09/25' },
  { name: '資安自評表.xlsx', detail: '204 KB · 09/18' },
];

/** The shared dialog structure in its three common sizes. */
@Component({
  selector: 'nx-design-dialogs',
  imports: [CompactDialog, DesignExample, DesignSection, DialogHeader, Field, Icon, SearchField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    ul {
      display: grid;
      gap: 1px;
      margin: 0;
      padding: var(--p-space-2);
      list-style: none;
    }
    li > button {
      display: grid;
      width: 100%;
      padding: var(--p-space-2);
      border-radius: var(--p-radius-md);
      text-align: start;
    }
    li > button:hover {
      background: var(--hover);
    }
    li small,
    .design-empty {
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    .design-empty {
      margin: 0;
      padding: var(--p-space-4);
    }
  `,
  template: `<nx-design-section
      id="design-dialog"
      title="對話框"
      api="nxCompactDialog size · nx-dialog-header · .dialog-body · .dialog-foot · .dialog-note"
      description="一個結構：標題列（nx-dialog-header，自帶關閉）、只有內容區捲動、選用頁尾。sm、md 無分隔線；lg、full 以分隔線切開三區。640px 以下一律變底部 sheet，可由標題列下拉關閉。"
    >
      <nx-design-example label="sm · 確認或單一欄位">
        <button type="button" class="secondary-button" (click)="show(small)">開啟 sm</button>
      </nx-design-example>
      <nx-design-example label="md · 表單加說明">
        <button type="button" class="secondary-button" (click)="show(form)">開啟 md</button>
      </nx-design-example>
      <nx-design-example label="lg · 工具列加清單">
        <button type="button" class="secondary-button" (click)="show(picker)">
          <nx-icon name="files" />開啟 lg
        </button>
      </nx-design-example>
    </nx-design-section>
    <dialog nxCompactDialog size="sm" #small class="platform-dialog">
      <nx-dialog-header heading="刪除示範項目？" closeLabel="關閉示範確認" />
      <div class="dialog-body">
        <p>刪除後無法復原。</p>
      </div>
      <footer class="dialog-foot">
        <button type="button" class="secondary-button" (click)="small.close()">取消</button>
        <button type="button" class="danger-button" (click)="small.close()">刪除</button>
      </footer>
    </dialog>
    <dialog nxCompactDialog size="md" #form class="platform-dialog">
      <form class="dialog-form" (submit)="$event.preventDefault(); form.close()">
        <nx-dialog-header heading="新增知識庫" closeLabel="關閉示範表單" />
        <div class="dialog-body">
          <label>名稱<input nxField required /></label>
          <label>說明<textarea nxField></textarea></label>
        </div>
        <footer class="dialog-foot">
          <p class="dialog-note">建立後可再加入文件與設定存取權限。</p>
          <button type="button" class="secondary-button" (click)="form.close()">取消</button>
          <button class="primary-button">建立</button>
        </footer>
      </form>
    </dialog>
    <dialog nxCompactDialog size="lg" #picker class="platform-dialog">
      <nx-dialog-header
        heading="選取檔案"
        [detail]="matches().length + ' 份'"
        closeLabel="關閉示範選取"
      >
        <nx-search-field
          appearance="bare"
          label="搜尋示範檔案"
          [value]="query()"
          (valueChange)="query.set($event)"
        />
      </nx-dialog-header>
      <div class="dialog-body dialog-body-flush">
        @if (matches().length) {
          <ul>
            @for (file of matches(); track file.name) {
              <li>
                <button type="button" (click)="pick(picker, file.name)">
                  {{ file.name }}<small>{{ file.detail }}</small>
                </button>
              </li>
            }
          </ul>
        } @else {
          <p class="design-empty">沒有符合的示範檔案。</p>
        }
      </div>
    </dialog>`,
})
export class DesignDialogs {
  private readonly toast = inject(ToastService);
  readonly query = signal('');
  readonly matches = computed(() => {
    const query = this.query().trim().toLowerCase();
    return FILES.filter((file) => file.name.toLowerCase().includes(query));
  });
  show(dialog: HTMLDialogElement) {
    this.query.set('');
    dialog.showModal();
  }
  pick(dialog: HTMLDialogElement, name: string) {
    dialog.close();
    this.toast.success('已選取示範檔案', name);
  }
}
