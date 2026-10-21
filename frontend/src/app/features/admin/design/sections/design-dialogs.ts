import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TESTING_IDENTITY_NOTE } from '@core/auth/workspace-session';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { SearchField } from '@shared/ui/search-field';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ToastService } from '@shared/ui/toast-service';
import { DesignExample, DesignSection } from './design-section';

const FILES = [
  { name: '季度營運報告.pdf', detail: '2.4 MB · 10/08' },
  { name: '產品路線圖.xlsx', detail: '860 KB · 10/06' },
  { name: '客服常見問答.docx', detail: '412 KB · 10/03' },
  { name: '設計規範.md', detail: '38 KB · 09/29' },
  { name: '會議紀錄 0925.txt', detail: '12 KB · 09/25' },
  { name: '資安自評表.xlsx', detail: '204 KB · 09/18' },
];

const SAVED = { name: '示範角色', note: '可檢視知識庫與專案。' };

/** 三種常見尺寸的共用對話框結構，以及設定表單的儲存列。 */
@Component({
  selector: 'nx-design-dialogs',
  imports: [
    CompactDialog,
    DesignExample,
    DesignSection,
    DialogHeader,
    DialogSaveBar,
    Field,
    Icon,
    SearchField,
    Tabs,
  ],
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
      border-radius: var(--radius-item);
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
    nx-design-example:has(.design-save-frame) {
      flex: 1 1 20rem;
    }
    .design-save-frame {
      width: 100%;
      border: 1px solid var(--line);
      border-radius: var(--dialog-radius);
      background: var(--surface);
    }
    .design-save-frame .dialog-body {
      overflow: visible;
    }
  `,
  template: `<nx-design-section
      id="design-dialog"
      title="對話框尺寸"
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
    <nx-design-section
      id="design-save-bar"
      title="儲存列"
      api="nx-dialog-save-bar · TabItem.dirty"
      description="編輯已儲存內容的頁尾：說明｜還原｜儲存，按鈕位置固定。未變更時兩鈕停用；有變更時說明轉色，所在頁籤加圓點（名稱加「有未儲存的變更」）；不能寫入時 note 取代說明，儲存停用、還原仍可用。"
    >
      <nx-design-example label="未變更 → 有變更">
        <form class="design-save-frame" (submit)="save($event)">
          <nx-tabs
            #sections
            label="示範設定分類"
            [items]="tabs()"
            [value]="tab()"
            (valueChange)="tab.set($event)"
          />
          <div
            class="dialog-body"
            role="tabpanel"
            [id]="sections.panelId(tab())"
            [attr.aria-labelledby]="sections.tabId(tab())"
          >
            @if (tab() === 'name') {
              <label
                >角色名稱<input
                  nxField
                  [value]="draft().name"
                  (input)="edit('name', $any($event.target).value)"
              /></label>
            } @else {
              <label
                >說明<input
                  nxField
                  [value]="draft().note"
                  (input)="edit('note', $any($event.target).value)"
              /></label>
            }
          </div>
          <nx-dialog-save-bar label="儲存角色" [dirty]="dirty()" (revert)="draft.set(saved())" />
        </form>
      </nx-design-example>
      <nx-design-example label="不能寫入（測試身分）">
        <form class="design-save-frame" (submit)="$event.preventDefault()">
          <div class="dialog-body">
            <label>角色名稱<input nxField value="已修改的名稱" readonly /></label>
          </div>
          <nx-dialog-save-bar
            label="儲存角色"
            [dirty]="true"
            [disabled]="true"
            [note]="lockedNote"
          />
        </form>
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
      <form class="dialog-form" (submit)="closeSubmittedDialog($event, form)">
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
  readonly lockedNote = TESTING_IDENTITY_NOTE;
  readonly saved = signal(SAVED);
  readonly draft = signal(SAVED);
  readonly tab = signal('name');
  readonly dirty = computed(() => this.tabs().some((tab) => tab.dirty));
  /** 一次儲存涵蓋兩個分頁，因此各自標記其持有的變更。 */
  readonly tabs = computed<TabItem[]>(() => [
    { value: 'name', label: '名稱', dirty: this.draft().name !== this.saved().name },
    { value: 'note', label: '說明', dirty: this.draft().note !== this.saved().note },
  ]);
  readonly query = signal('');
  readonly matches = computed(() => {
    const query = this.query().trim().toLowerCase();
    return FILES.filter((file) => file.name.toLowerCase().includes(query));
  });
  closeSubmittedDialog(event: Event, dialog: HTMLDialogElement) {
    event.preventDefault();
    dialog.close();
  }
  show(dialog: HTMLDialogElement) {
    this.query.set('');
    dialog.showModal();
  }
  edit(key: keyof typeof SAVED, value: string) {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }
  save(event: Event) {
    event.preventDefault();
    this.saved.set(this.draft());
    this.toast.success('已儲存示範角色');
  }
  pick(dialog: HTMLDialogElement, name: string) {
    dialog.close();
    this.toast.success('已選取示範檔案', name);
  }
}
