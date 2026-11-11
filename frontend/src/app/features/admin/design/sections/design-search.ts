import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { DialogHeader } from '@shared/ui/dialog-header';
import { SearchField } from '@shared/ui/search-field';
import { Select, type SelectOption } from '@shared/ui/select';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DesignExample, DesignSection } from './design-section';

/**
 * 搜尋的三種外觀，各放在它實際所在的位置：工具列（與 segment、膠囊 select 並排）、浮層頂端、對話框標題列。
 * 放在「操作與選取」而不是「表單」：搜尋立即改變畫面，不是要送出的值。
 */
@Component({
  selector: 'nx-design-search',
  imports: [DesignExample, DesignSection, DialogHeader, SearchField, Select, ViewSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    /* 區段直接排進操作與選取的版面，不另成一層。 */
    :host {
      display: contents;
    }
    nx-design-example {
      flex: 1 1 14rem;
    }
    nx-design-example > * {
      width: 100%;
    }
    // 工具列獨占一行：三種工具並排才看得出同為膠囊、等高。
    .design-toolbar {
      display: flex;
      flex-wrap: wrap;
      flex-basis: 100%;
      align-items: center;
      gap: var(--p-space-2);
      min-width: 0;
    }
    .design-toolbar > nx-view-switch {
      margin-bottom: 0;
    }
    .design-toolbar > nx-search-field {
      flex: 1 1 14rem;
    }
    // 對話框縮影要有標題、搜尋與關閉並排的寬度。
    nx-design-example.design-dialog-example {
      flex: 2 1 26rem;
    }
    // 浮層與對話框的靜態縮影：外框取正式的角色，裡面是正式的搜尋、選項列與標題列，看到的就是實際長相。
    .design-popover {
      width: min(100%, 16rem);
      border: 1px solid var(--line);
      border-radius: var(--radius-surface);
      background: var(--surface);
      box-shadow: var(--shadow-floating);
    }
    .design-dialog {
      width: min(100%, 28rem);
      overflow: hidden;
      border: 1px solid var(--line);
      border-radius: var(--dialog-radius);
      background: var(--surface);
    }
    // 與 lg 對話框相同：標題列上下等距，與內容之間一道細線（styles/dialogs.scss）。
    .design-dialog > .dialog-bar {
      min-height: calc(var(--button-target) + 2 * var(--p-space-2));
      padding-block: var(--p-space-2);
      border-bottom: 1px solid var(--line);
    }
    .design-dialog > p {
      margin: 0;
      padding: var(--p-space-3) var(--p-space-5);
      color: var(--secondary);
      font-size: var(--text-caption);
    }
  `,
  template: `<nx-design-section
      id="design-search"
      title="工具列的搜尋與篩選"
      api='SearchField · Select appearance="pill" · ViewSwitch appearance="segment"'
      description="立即改變畫面的工具（搜尋、篩選、切換）是膠囊，並排時同高同形；填好才送出的值是 6px 欄位（見「表單」）。外框、焦點與停用和欄位共用。打字即搜尋時傳 busy：放大鏡換成轉圈，仍可輸入。"
    >
      <div class="design-toolbar">
        <nx-view-switch
          label="示範範圍"
          appearance="segment"
          [options]="scopes"
          [value]="scope()"
          (valueChange)="scope.set($event)"
        />
        <nx-search-field
          label="示範搜尋"
          placeholder="搜尋文件與對話"
          [value]="search()"
          (valueChange)="search.set($event)"
        />
        <nx-select
          appearance="pill"
          label="示範排序"
          [options]="orders"
          [value]="order()"
          (valueChange)="order.set($event)"
        />
      </div>
      <nx-design-example label="busy">
        <nx-search-field label="示範搜尋中" value="星河" [busy]="true" />
      </nx-design-example>
      <nx-design-example label="停用">
        <nx-search-field label="示範停用的搜尋" placeholder="搜尋文件與對話" [disabled]="true" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-search-embedded"
      title="浮層與標題列的搜尋"
      api='SearchField appearance="filled" · "bare"'
      description="放進已有外框的地方不再加框。浮層裡、側欄與清單窗格頂端用 filled（在浮層頂端加 popover-search）：淡底圓角，放大鏡對齊下方選項。所在的列就是外框時（對話框標題列、浮動工具列）用 bare。帶動清單時給 controls，輸入框成為 combobox。"
    >
      <nx-design-example label="filled（浮層、側欄頂端）">
        <div class="design-popover">
          <nx-search-field
            class="popover-search"
            appearance="filled"
            label="示範浮層搜尋"
            placeholder="搜尋狀態"
            [value]="filledSearch()"
            (valueChange)="filledSearch.set($event)"
          />
          <div class="option-list">
            @for (status of filledMatches(); track status) {
              <button type="button" class="option-row">
                <span class="option-text"
                  ><span class="option-label">{{ status }}</span></span
                >
              </button>
            } @empty {
              <p class="option-empty">沒有符合的狀態</p>
            }
          </div>
        </div>
      </nx-design-example>
      <nx-design-example class="design-dialog-example" label="bare（對話框標題列）">
        <div class="design-dialog">
          <nx-dialog-header
            heading="選取檔案"
            [detail]="bareMatches().length + ' 份'"
            closeLabel="關閉示範對話框"
          >
            <nx-search-field
              appearance="bare"
              label="示範列內搜尋"
              placeholder="搜尋檔案"
              [value]="bareSearch()"
              (valueChange)="bareSearch.set($event)"
            />
          </nx-dialog-header>
          <p>{{ bareMatches().join('、') || '沒有符合的檔案' }}</p>
        </div>
      </nx-design-example>
    </nx-design-section>`,
})
export class DesignSearch {
  readonly scope = signal('all');
  readonly scopes = [
    { value: 'all', label: '全部' },
    { value: 'mine', label: '我的' },
    { value: 'shared', label: '共用' },
  ];
  readonly order = signal('recent');
  readonly orders: SelectOption[] = [
    { value: 'recent', label: '最近更新' },
    { value: 'name', label: '名稱' },
  ];
  readonly search = signal('');
  readonly filledSearch = signal('');
  readonly filledMatches = computed(() =>
    ['全部狀態', '進行中', '已完成', '已封存'].filter((status) =>
      status.includes(this.filledSearch().trim()),
    ),
  );
  readonly bareSearch = signal('');
  readonly bareMatches = computed(() =>
    ['季度營運報告.pdf', '產品路線圖.xlsx', '客服常見問答.docx'].filter((name) =>
      name.toLowerCase().includes(this.bareSearch().trim().toLowerCase()),
    ),
  );
}
