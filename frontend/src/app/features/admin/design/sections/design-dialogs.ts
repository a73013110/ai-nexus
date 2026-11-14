import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogActions } from '@shared/ui/dialog-actions';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { FloatingToolbar } from '@shared/ui/floating-toolbar';
import { Icon } from '@shared/ui/icon';
import { IdleChrome } from '@shared/ui/idle-chrome';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
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
    CopyValue,
    DesignExample,
    DesignSection,
    DialogActions,
    DialogHeader,
    DialogSaveBar,
    Field,
    FloatingToolbar,
    Icon,
    IdleChrome,
    SearchField,
    StatusBadge,
    Tabs,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    /* 區段直接排進操作與選取的版面，不另成一層。 */
    :host {
      display: contents;
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
    .design-viewer {
      position: relative;
      flex: 1;
      min-height: 0;
      overflow: auto;
      background: var(--canvas);
    }
    .design-viewer p {
      max-width: 40rem;
      margin: 0 auto;
      padding: calc(var(--floating-toolbar-offset) + var(--button-target) + var(--p-space-6))
        var(--p-space-5) var(--p-space-6);
      color: var(--secondary);
    }
  `,
  template: `<nx-design-section
      id="design-dialog"
      title="對話框尺寸"
      api="nxCompactDialog size · nx-dialog-header · .dialog-body · nx-dialog-actions · .dialog-foot · .dialog-note"
      description="一個結構：標題列（nx-dialog-header，自帶關閉）、只有內容區捲動、選用頁尾。sm、md 無分隔線。lg、full 以分隔線切開三區。對話框從開啟它的按鈕長出來、關閉縮回去。檢視器（nxIdleChrome）的標題列不佔版面，標題與視窗動作各成一顆膠囊浮在內容上，放大只改外框。640px 以下一律變底部 sheet，可由標題列下拉關閉。"
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
      <nx-design-example label="lg · 單筆紀錄（icon＋dialog-subtitle）">
        <button type="button" class="secondary-button" (click)="show(record)">
          <nx-icon name="calendar" />開啟紀錄
        </button>
      </nx-design-example>
      <nx-design-example label="full · 檢視器（nxIdleChrome＋maximizable）">
        <button type="button" class="secondary-button" (click)="show(viewer)">
          <nx-icon name="maximize" />開啟檢視器
        </button>
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-save-bar"
      title="儲存列"
      api="nx-dialog-save-bar · TabItem.dirty"
      description="編輯已儲存內容的頁尾：說明｜還原｜儲存，按鈕位置固定。未變更時兩鈕停用。有變更時說明轉色，所在頁籤加圓點（名稱加「有未儲存的變更」）。note 可取代說明（例如儲存結果）。disabled 只停用儲存，還原仍可用。"
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
        <nx-dialog-actions label="建立" pendingLabel="建立中…" (dismiss)="form.close()">
          <p class="dialog-note">建立後可再加入文件與設定存取權限。</p>
        </nx-dialog-actions>
      </form>
    </dialog>
    <dialog nxCompactDialog size="lg" #record class="platform-dialog">
      <nx-dialog-header heading="病假" icon="calendar" closeLabel="關閉示範紀錄">
        <nx-status-badge dialog-title tone="warning">流程中</nx-status-badge>
        <p dialog-subtitle class="dialog-subtitle">
          <span>請假單</span>
          <nx-copy-value label="單號" value="L115102201" />
          <span>2026/10/22（四）08:45 申請</span>
        </p>
      </nx-dialog-header>
      <div class="dialog-body">
        <p>標題列左側是種類圖示，第二行寫種類、單號（可複製）與時間。工具與關閉鈕對齊第一行。</p>
      </div>
    </dialog>
    <dialog nxCompactDialog nxIdleChrome size="full" #viewer class="platform-dialog">
      <nx-dialog-header
        heading="季度營運報告.pdf"
        detail="PDF 文件 · 12 頁"
        closeLabel="關閉示範檢視器"
        maximizable
      />
      <div class="design-viewer">
        <nx-floating-toolbar label="示範檢視工具">
          <nx-view-switch
            label="示範檢視方式"
            appearance="segment"
            [options]="views"
            [value]="view()"
            (valueChange)="view.set($event)"
          />
        </nx-floating-toolbar>
        <p>
          標題與視窗動作是浮在內容上的兩顆膠囊，與工具列一起在閒置約 1.5 秒後淡出，指標移近上緣、Tab
          或觸控點一下內容再出現。寬螢幕上三者同一列，視窗較窄時工具列排到下一列。放大只改外框，版面不變。
        </p>
      </div>
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
  readonly saved = signal(SAVED);
  readonly draft = signal(SAVED);
  readonly tab = signal('name');
  readonly dirty = computed(() => this.tabs().some((tab) => tab.dirty));
  /** 一次儲存涵蓋兩個分頁，因此各自標記其持有的變更。 */
  readonly tabs = computed<TabItem[]>(() => [
    { value: 'name', label: '名稱', dirty: this.draft().name !== this.saved().name },
    { value: 'note', label: '說明', dirty: this.draft().note !== this.saved().note },
  ]);
  readonly views = [
    { value: 'original', label: '原始文件' },
    { value: 'text', label: '擷取文字' },
  ];
  readonly view = signal('original');
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
