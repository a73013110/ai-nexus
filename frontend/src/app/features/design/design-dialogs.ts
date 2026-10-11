import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CompactDialog } from '../../shared/ui/compact-dialog';
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

/** The flush picker layout and the phone bottom sheet, both on the shared dialog frame. */
@Component({
  selector: 'nx-design-dialogs',
  imports: [CompactDialog, DesignExample, DesignSection, Icon, SearchField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    .design-flush {
      --dialog-width: 30rem;
    }
    ul {
      display: grid;
      gap: 1px;
      margin: 0;
      padding: 0;
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
    small,
    .design-empty {
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    .design-empty {
      margin: 0;
      padding: var(--p-space-4) var(--p-space-2);
    }
  `,
  template: `<nx-design-section
      id="design-flush"
      title="扁平對話框"
      api=".dialog-flush · .dialog-bar · .dialog-body · .dialog-foot"
      description="選取器與清單用：標題列放標題或搜尋＋關閉，不加眉標；只有內容區捲動；頁尾放必要的數量或分頁。表單仍用 .dialog-scroll。"
    >
      <nx-design-example label="搜尋列＋頁尾">
        <button type="button" class="secondary-button" (click)="show(picker)">
          <nx-icon name="files" />開啟選取對話框
        </button>
      </nx-design-example>
      <nx-design-example label="標題列＋說明">
        <button type="button" class="secondary-button" (click)="show(titled)">
          <nx-icon name="list" />開啟清單對話框
        </button>
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-sheet"
      title="底部 sheet"
      api=".platform-dialog · .workspace-dialog"
      description="640px 以下，所有置中對話框改為全寬底部 sheet，由下方升起、不回彈（快捷指令例外）。桌面寬度看到的是置中對話框，請縮窄視窗或用裝置模擬檢視。"
    >
      <button type="button" class="secondary-button" (click)="show(sheet)">開啟表單對話框</button>
    </nx-design-section>
    <dialog
      nxCompactDialog
      #picker
      class="platform-dialog dialog-flush design-flush"
      aria-label="選取示範檔案"
    >
      <div class="dialog-bar">
        <nx-search-field
          appearance="bare"
          label="搜尋示範檔案"
          [value]="query()"
          (valueChange)="query.set($event)"
        />
        <button
          type="button"
          class="icon-button"
          aria-label="關閉示範選取"
          (click)="picker.close()"
        >
          <nx-icon name="close" />
        </button>
      </div>
      <div class="dialog-body">
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
      <div class="dialog-foot">
        <span>{{ matches().length }} 份檔案</span>
      </div>
    </dialog>
    <dialog nxCompactDialog #titled class="platform-dialog dialog-flush design-flush">
      <header class="dialog-bar">
        <div class="dialog-title">
          <h2>最近檔案</h2>
          <small>{{ files.length }} 份</small>
        </div>
        <button
          type="button"
          class="icon-button"
          aria-label="關閉示範清單"
          (click)="titled.close()"
        >
          <nx-icon name="close" />
        </button>
      </header>
      <div class="dialog-body">
        <ul>
          @for (file of files; track file.name) {
            <li>
              <button type="button" (click)="pick(titled, file.name)">
                {{ file.name }}<small>{{ file.detail }}</small>
              </button>
            </li>
          }
        </ul>
      </div>
    </dialog>
    <dialog nxCompactDialog #sheet class="platform-dialog">
      <div class="dialog-scroll">
        <div class="dialog-heading">
          <h2>表單示範</h2>
          <button
            type="button"
            class="icon-button"
            aria-label="關閉表單示範"
            (click)="sheet.close()"
          >
            <nx-icon name="close" />
          </button>
        </div>
        <p class="form-note">
          在 640px 以下，這個對話框貼齊畫面底部、佔滿寬度，並保留裝置底部的安全區域。
        </p>
        <div class="dialog-actions">
          <button type="button" class="primary-button" (click)="sheet.close()">知道了</button>
        </div>
      </div>
    </dialog>`,
})
export class DesignDialogs {
  private readonly toast = inject(ToastService);
  readonly files = FILES;
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
