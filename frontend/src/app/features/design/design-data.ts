import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CodeBlock } from '@shared/markdown/code-block';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { CopyValue } from '@shared/ui/copy-value';
import { DataTable, DataTableRow, TablePagination, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { QueryBar } from '@shared/ui/query-bar';
import { QueryState } from '@shared/ui/query-state';
import { Panel } from '@shared/ui/panel';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { StatusBadge } from '@shared/ui/status-badge';
import { Tabs } from '@shared/ui/tabs';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DesignExample, DesignSection } from './design-section';

/** The compact data workspace (filters, table, detail drawer) and the reading components. */
@Component({
  selector: 'nx-design-data',
  imports: [
    CodeBlock,
    CopyValue,
    DataTable,
    DataTableRow,
    DesignExample,
    DesignSection,
    DetailDrawer,
    DrawerPager,
    Field,
    Icon,
    MarkdownView,
    Panel,
    QueryBar,
    ResourceCard,
    ResourceGallery,
    StatusBadge,
    TablePagination,
    TableState,
    Tabs,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    .design-data-workspace nx-view-switch {
      margin-bottom: 0;
    }
    .design-table-states {
      margin-bottom: var(--p-space-2);
    }
    nx-design-example {
      flex: 1 1 22rem;
    }
    .design-data-workspace nx-data-table {
      --table-min-width: 32rem;
    }
    nx-markdown-view {
      display: block;
      max-width: var(--reading-width);
    }
    .design-resources {
      --gallery-min: 13rem;
    }
  `,
  template: `<nx-design-section
      id="design-table"
      class="design-data-workspace ui-density-compact"
      title="資料工作區"
      api="QueryBar · DataTable · TableState · TablePagination · DetailDrawer · DrawerPager · CopyValue"
      description="查詢列分開草稿與已套用條件：改了沒按「查詢」會提示，已套用的條件成為可逐一移除的 chip。表格獨立捲動、固定表頭，匯出放工具列「欄位」旁；載入、無資料與失敗由 TableState 呈現。整列可點，hover 淡底、開啟中的列為選取色，鍵盤焦點只在列外框一圈；詳情在側欄開啟、可切換上下筆、識別碼可複製，窄螢幕改為 modal。加 stacked 的表格在窄螢幕改為卡片列。"
      [stacked]="true"
    >
      <nx-query-bar label="示範查詢條件" [state]="query">
        <nx-view-switch
          query-lead
          label="資料範例檢視"
          [options]="dataViews"
          [value]="query.applied().view"
          (valueChange)="query.commit({ view: $event })"
        />
        <label class="ui-field ui-query-wide"
          >事件名稱<input
            nxField
            placeholder="依事件名稱查詢"
            [value]="query.draft().event"
            (input)="query.set('event', $any($event.target).value)"
        /></label>
      </nx-query-bar>
      <nx-view-switch
        class="design-table-states"
        label="表格狀態示範"
        [options]="tableStates"
        [value]="tableState()"
        (valueChange)="tableState.set($event)"
      />
      <nx-data-table label="共用資料表範例" [stacked]="true" [busy]="tableState() === 'loading'">
        <div table-toolbar class="ui-table-toolbar">
          <h2>事件範例</h2>
        </div>
        <button table-actions type="button" class="quiet-button" (click)="exported()">
          <nx-icon name="download" />匯出 CSV
        </button>
        <table>
          <caption class="sr-only">
            共用資料表與狀態標籤範例
          </caption>
          <thead>
            <tr>
              <th scope="col">時間</th>
              <th scope="col">事件</th>
              <th scope="col">狀態</th>
            </tr>
          </thead>
          <tbody>
            @for (entry of shownRows(); track entry.event) {
              <tr
                nxTableRow
                [class.is-selected]="selected() === entry.event"
                (rowActivate)="inspect(entry.event)"
              >
                <td data-label="時間">{{ entry.time }}</td>
                <td>
                  <button
                    type="button"
                    class="ui-table-link"
                    data-row-action
                    [attr.aria-expanded]="selected() === entry.event"
                    (click)="inspect(entry.event)"
                  >
                    {{ entry.event }}
                  </button>
                </td>
                <td data-stack="end">
                  <nx-status-badge [tone]="entry.tone">{{ entry.status }}</nx-status-badge>
                </td>
              </tr>
            }
          </tbody>
        </table>
        <nx-table-state
          [count]="shownRows().length"
          [loading]="tableState() === 'loading'"
          [error]="tableState() === 'error' ? '示範：服務暫時無法使用。' : ''"
          emptyTitle="沒有符合條件的事件"
          emptyText="試著擴大時間範圍，或移除部分條件。"
          (retry)="tableState.set('rows')"
        />
      </nx-data-table>
    </nx-design-section>
    <nx-design-section
      id="design-pagination"
      title="分頁"
      api='TablePagination mode="cursor" · mode="offset"'
      description="cursor 用於大表（日誌、稽核）：不計總數、不跳頁，只有上下頁與回到第一頁，以時間範圍與排序定位。offset 用於有總數的小表（使用者），可輸入頁碼跳轉。"
    >
      <nx-design-example label="cursor">
        <nx-table-pagination
          [pageIndex]="cursorPage()"
          [pageSize]="50"
          [count]="50"
          [hasNext]="cursorPage() < 2"
          (pageChange)="cursorPage.set(cursorPage() + $event)"
          (pageSelect)="cursorPage.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="offset">
        <nx-table-pagination
          mode="offset"
          [pageIndex]="offsetPage()"
          [pageSize]="100"
          [pageSizes]="[100]"
          [total]="523"
          [count]="offsetPage() === 5 ? 23 : 100"
          (pageChange)="offsetPage.set(offsetPage() + $event)"
          (pageSelect)="offsetPage.set($event)"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-resources"
      class="design-resources ui-density-compact"
      title="資源集合"
      api="ResourceGallery · ResourceCard · Panel"
      description="檔案庫、知識庫、專案與成果共用：列表是膠囊工具列與卡片格線，詳細頁由 Panel 組成。"
      [stacked]="true"
    >
      <nx-resource-gallery
        label="範例資源"
        placeholder="尋找範例"
        [filters]="dataViews"
        [filter]="dataView()"
        [search]="term()"
        [empty]="!cards().length"
        (filterChange)="dataView.set($event)"
        (searchChange)="term.set($event)"
      >
        @for (card of cards(); track card.name) {
          <a
            nxResourceCard
            href="#design-resources"
            icon="library"
            [heading]="card.name"
            [description]="card.description"
            ><span>{{ card.meta }}</span
            ><span>3 天前更新</span></a
          >
        }
        <p gallery-empty class="form-note">沒有符合的範例。</p>
      </nx-resource-gallery>
      <nx-panel heading="來源文件" icon="document" [count]="2">
        <button panel-actions class="icon-button" aria-label="範例動作">
          <nx-icon name="plus" />
        </button>
        <p class="form-note">Panel 的標題、數量與右側動作；內容由功能決定。</p>
      </nx-panel>
    </nx-design-section>
    <nx-design-section
      id="design-markdown"
      class="design-markdown"
      title="內容閱讀"
      api="MarkdownView · CodeBlock"
      description="聊天、文件與成果共用同一套閱讀元件；字級與行距跟隨個人設定。"
      [stacked]="true"
    >
      <nx-markdown-view [content]="markdown" />
    </nx-design-section>
    <nx-detail-drawer
      title="資料詳情示範"
      [attr.data-theme]="theme()"
      (closed)="selected.set(null)"
    >
      <nx-drawer-pager
        drawer-actions
        noun="事件"
        [index]="selectedIndex()"
        [count]="shownRows().length"
        (move)="inspectAdjacent($event)"
      />
      <div drawer-summary class="ui-drawer-summary">
        <h3>{{ event() }}</h3>
        <span class="ui-drawer-subtitle">共用詳情面板 · 本地範例</span>
      </div>
      <nx-tabs
        #detailTabs
        drawer-tabs
        label="資料詳情示範內容"
        [items]="tabs"
        [value]="tab()"
        (valueChange)="tab.set($event)"
      />
      <section
        role="tabpanel"
        tabindex="0"
        [id]="detailTabs.panelId(tab())"
        [attr.aria-labelledby]="detailTabs.tabId(tab())"
      >
        @if (tab() === 'overview') {
          <h3 class="ui-section-heading">事件</h3>
          <p class="ui-message-box">{{ event() }}</p>
          <h3 class="ui-section-heading">執行資訊</h3>
          <dl class="ui-description-list">
            <div>
              <dt>服務</dt>
              <dd>AiNexus.Host</dd>
            </div>
            <div>
              <dt>來源</dt>
              <dd>介面元件範例</dd>
            </div>
            <div class="ui-description-wide">
              <dt>TraceId</dt>
              <dd><nx-copy-value label="TraceId" [value]="traceId" /></dd>
            </div>
          </dl>
        } @else {
          <nx-code-block label="示範屬性 JSON" [value]="properties" />
        }
      </section>
    </nx-detail-drawer>`,
})
export class DesignData {
  readonly theme = input<'light' | 'dark'>('light');
  readonly dataView = signal('all');
  readonly dataViews = [
    { value: 'all', label: '全部狀態' },
    { value: 'success', label: '已完成' },
  ];
  readonly term = signal('');
  readonly cards = computed(() =>
    [
      {
        name: '公司作業規範',
        description: '同事共用的規章與表單，引用可回到原檔頁碼。',
        meta: '我擁有',
      },
      { name: '產品技術文件', description: '', meta: '共用 · 可閱讀' },
    ].filter((card) => card.name.includes(this.term())),
  );
  readonly tab = signal('overview');
  readonly tabs = [
    { value: 'overview', label: '概覽' },
    { value: 'properties', label: '屬性' },
  ];
  readonly rows = [
    {
      event: 'knowledge.index.completed',
      time: '10:42:18',
      status: '完成',
      tone: 'success' as const,
    },
    { event: 'inference.request.started', time: '10:42:06', status: '資訊', tone: 'info' as const },
    { event: 'provider.request.retry', time: '10:41:55', status: '重試', tone: 'warning' as const },
  ];
  readonly event = signal(this.rows[0].event);
  /** The row whose detail is open; the drawer keeps its content while it leaves. */
  readonly selected = signal<string | null>(null);
  /** Local only: the workbench keeps its URL, so the state has no router. */
  readonly query = new QueryState<{ view: string; event: string }>({
    defaults: () => ({ view: 'all', event: '' }),
    chips: (query) =>
      query.event.trim()
        ? [{ id: 'event', label: '事件：' + query.event.trim(), clear: { event: '' } }]
        : [],
  });
  readonly tableStates = [
    { value: 'rows', label: '資料' },
    { value: 'loading', label: '載入' },
    { value: 'empty', label: '無資料' },
    { value: 'error', label: '失敗' },
  ];
  readonly tableState = signal('rows');
  readonly cursorPage = signal(0);
  readonly offsetPage = signal(0);
  readonly traceId = '4bf92f3577b34da6a3ce929d0e0e4736';
  readonly visibleRows = computed(() => {
    const { view, event } = this.query.applied();
    return this.rows.filter(
      (row) => (view === 'all' || row.tone === 'success') && row.event.includes(event.trim()),
    );
  });
  readonly shownRows = computed(() => (this.tableState() === 'rows' ? this.visibleRows() : []));
  readonly selectedIndex = computed(() =>
    this.shownRows().findIndex((row) => row.event === this.selected()),
  );
  private readonly toast = inject(ToastService);
  readonly properties = JSON.stringify(
    { service: 'AiNexus.Host', attempts: 2, state: 'completed' },
    null,
    2,
  );
  readonly markdown =
    '### 好的回答，也需要好的閱讀介面\n\n文字有清楚的層次，資料有可以核對的來源。**段落、表格與程式碼**沿用同一套閱讀元件。\n\n| 項目 | 用途 |\n| --- | --- |\n| 文件來源 | 核對原始資訊 |\n| 成果版本 | 保存每次異動 |\n\n```typescript\nconst workspace = { clarity: true };\n```\n\n> 先讓使用者看得清楚，再讓操作自然發生。';
  private readonly drawer = viewChild.required(DetailDrawer);
  inspectAdjacent(direction: -1 | 1) {
    const row = this.shownRows()[this.selectedIndex() + direction];
    if (row) this.inspect(row.event);
  }
  exported() {
    this.toast.success('示範匯出', '實際頁面依已套用的條件匯出。');
  }
  inspect(event: string) {
    this.event.set(event);
    this.selected.set(event);
    this.drawer().open();
  }
}
