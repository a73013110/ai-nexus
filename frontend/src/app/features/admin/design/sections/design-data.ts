import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { ViewScope } from '@shared/browser/view-scope';
import { applySortWrites, bySortOrder, sortWrites } from '@shared/browser/sort-order';
import { CodeBlock } from '@shared/markdown/code-block';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { ChangeList, FieldChange } from '@shared/ui/change-list';
import { CopyValue } from '@shared/ui/copy-value';
import { DataTable, DataTableRow, TablePagination, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { QueryBar, QueryField } from '@shared/ui/query-bar';
import { QueryState } from '@shared/ui/query-state';
import { Panel } from '@shared/ui/panel';
import { PropertyGroup, PropertyList } from '@shared/ui/property-list';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { SearchField } from '@shared/ui/search-field';
import { SortableRows, SortHandle, SortRank, type RowMove } from '@shared/ui/sortable-rows';
import { StatusBadge } from '@shared/ui/status-badge';
import { Tabs } from '@shared/ui/tabs';
import { ToastService } from '@shared/ui/toast-service';
import { TreeItem, TreeView } from '@shared/ui/tree-view';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DesignExample, DesignSection } from './design-section';

/** 展開時才「載入」的下層，模擬目錄或組織 API 的回應。 */
const LAZY_CHILDREN: Record<string, TreeItem[]> = {
  rd: [
    {
      id: 'rd-platform',
      label: '平台部',
      icon: 'container',
      count: 9,
      badge: 2,
      badgeLabel: '已選 2 人',
      hasChildren: false,
    },
    { id: 'rd-apps', label: '應用部', icon: 'container', count: 15, hasChildren: false },
  ],
  sales: [
    { id: 'sales-north', label: '北區業務組', icon: 'container', count: 12, hasChildren: false },
    {
      id: 'sales-pending',
      label: '待整併小組',
      icon: 'container',
      title: '目前沒有成員',
      count: 0,
      muted: true,
      hasChildren: false,
    },
  ],
};

/** 精簡的資料工作區（篩選、表格、詳情抽屜）與閱讀元件。 */
@Component({
  selector: 'nx-design-data',
  imports: [
    ChangeList,
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
    PropertyList,
    QueryBar,
    QueryField,
    ResourceCard,
    ResourceGallery,
    SearchField,
    SortableRows,
    SortHandle,
    SortRank,
    StatusBadge,
    TablePagination,
    TableState,
    Tabs,
    TreeView,
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
    nx-tree-view,
    .design-properties {
      width: min(100%, 20rem);
    }
  `,
  template: `<nx-design-section
      id="design-table"
      class="design-data-workspace ui-density-compact"
      title="資料工作區"
      api="QueryBar · DataTable · TableState · TablePagination · DetailDrawer · DrawerPager · CopyValue"
      description="查詢列分開草稿與已套用條件：改了沒按「查詢」會提示，已套用的條件成為可逐一移除的 chip，欄位標上 nxQueryField 時 chip 依欄位順序排列。表格獨立捲動、固定表頭，匯出放工具列「欄位」旁；首次載入、無資料與失敗由 TableState 呈現，已有列時重讀（busy）保留舊列、淡化並在上緣掃過細線，失敗的重試鈕在重讀中轉圈（retrying）。整列可點，hover 淡底、開啟中的列為選取色，鍵盤焦點只在列外框一圈；詳情在側欄開啟、可切換上下筆、識別碼可複製，窄螢幕改為 modal。加 stacked 的表格在窄螢幕改為卡片列。"
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
        <label class="ui-field ui-query-wide" nxQueryField="event"
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
      <nx-data-table label="共用資料表範例" [stacked]="true" [busy]="tableBusy()">
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
          [retrying]="retrying()"
          [error]="tableState() === 'error' ? '示範：服務暫時無法使用。' : ''"
          emptyTitle="沒有符合條件的事件"
          emptyText="試著擴大時間範圍，或移除部分條件。"
          (retry)="retry()"
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
      description="檔案庫、知識庫、專案與成果共用：列表是膠囊工具列與卡片格線，詳細頁由 Panel 組成。首次載入（loading）是卡片骨架；已有卡片時重讀（busy）保留舊卡片、淡化並在上緣掃過細線，搜尋欄同時轉圈。"
      [stacked]="true"
    >
      <nx-view-switch
        class="design-table-states"
        label="集合狀態示範"
        [options]="galleryStates"
        [value]="galleryState()"
        (valueChange)="galleryState.set($event)"
      />
      <nx-resource-gallery
        label="範例資源"
        placeholder="尋找範例"
        [filters]="dataViews"
        [filter]="dataView()"
        [search]="term()"
        [loading]="galleryState() === 'loading'"
        [busy]="galleryState() === 'refreshing'"
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
      id="design-tree"
      title="樹狀瀏覽"
      api="TreeView"
      description="目錄、組織這類階層瀏覽：展開時才載入下層，節點旁可帶數量與已選小計，沒有成員的節點淡化。上下鍵移動、右鍵展開、左鍵收合或回上層，Enter 選取。"
    >
      <nx-design-example label="延遲載入、數量與已選">
        <nx-tree-view
          label="示範組織"
          [items]="tree()"
          [selected]="treeSelected()"
          [expanded]="treeOpen()"
          (nodeSelect)="treeSelected.set($event)"
          (nodeToggle)="toggleNode($event.id, $event.open)"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-sortable"
      title="手動排序"
      api="SortableRows、SortHandle、SortRank"
      description="使用者、選單這類由管理者決定先後的列：直接在表格上拖曳把手、在把手上按方向鍵，或按編號輸入第幾位；值依 sortWrites（伺服器 ManualOrder）計算，通常只寫被移動的那一筆。未排序的在虛線之下、編號淡化。搜尋時只能輸入編號。"
    >
      <nx-design-example label="已排序、未排序與搜尋">
        <nx-search-field
          label="搜尋示範名單"
          placeholder="姓名"
          [value]="sortFilter()"
          (valueChange)="sortFilter.set($event)"
        />
        <div class="ui-table-frame design-sort-table">
          <div class="ui-table-scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">順序</th>
                  <th scope="col">姓名</th>
                  <th scope="col">排序值</th>
                </tr>
              </thead>
              <tbody
                [nxSortableRows]="sortIds()"
                [sortDisabled]="!!sortFilter().trim()"
                [sortName]="sortName"
                (sortMove)="sortMove($event)"
              >
                @for (person of sortPeople(); track person.id; let index = $index) {
                  <tr
                    [attr.data-sort-id]="person.id"
                    [hidden]="!sortMatch(person)"
                    [class.is-first-unsorted]="
                      !sortFilter().trim() &&
                      person.sortOrder === null &&
                      index > 0 &&
                      sortPeople()[index - 1].sortOrder !== null
                    "
                  >
                    <td>
                      <nx-sort-handle
                        [id]="person.id"
                        [name]="person.name"
                        [position]="index + 1"
                        [count]="sortPeople().length"
                        [locked]="!!sortFilter().trim()"
                      /><nx-sort-rank
                        [name]="person.name"
                        [position]="index + 1"
                        [count]="sortPeople().length"
                        [unsorted]="person.sortOrder === null"
                        (place)="sortMove({ id: person.id, index: $event - 1 })"
                      />
                    </td>
                    <td>{{ person.name }}</td>
                    <td>{{ person.sortOrder ?? '未排序' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-changes"
      title="欄位變更"
      api="ChangeList"
      description="預覽寫入、同步差異與稽核前後值共用：舊值刪除線、新值淡底，原本沒有值只列新值，清空寫「（無）」。"
    >
      <nx-design-example label="改名、新增、清空">
        <nx-change-list [changes]="changes" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-properties"
      title="屬性清單"
      api="PropertyList"
      description="屬性多的詳情（AD 屬性、原始紀錄）分組列出：名稱旁附來源名稱，可篩選名稱或值並標出相符處；沒有值的屬性預設收起，整組共用的說明只寫一次。"
    >
      <nx-design-example label="分組、旗標、多值與識別碼">
        <nx-property-list class="design-properties" label="示範屬性" [groups]="propertyGroups" />
      </nx-design-example>
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
  readonly galleryStates = [
    { value: 'cards', label: '資料' },
    { value: 'loading', label: '載入' },
    { value: 'refreshing', label: '重讀' },
  ];
  readonly galleryState = signal('cards');
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
  /** 詳情已開啟的那一列；抽屜退場時會保留它的內容。 */
  readonly selected = signal<string | null>(null);
  /** 只存在本地：工作台保留自己的 URL，因此狀態不需要路由。 */
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
    { value: 'refreshing', label: '重讀' },
    { value: 'empty', label: '無資料' },
    { value: 'error', label: '失敗' },
  ];
  readonly tableState = signal('rows');
  /** 失敗後按了重試、示範讀取還沒回來。 */
  readonly retrying = signal(false);
  readonly tableBusy = computed(
    () => this.tableState() === 'loading' || this.tableState() === 'refreshing' || this.retrying(),
  );
  readonly cursorPage = signal(0);
  readonly offsetPage = signal(0);
  readonly traceId = '4bf92f3577b34da6a3ce929d0e0e4736';
  readonly visibleRows = computed(() => {
    const { view, event } = this.query.applied();
    return this.rows.filter(
      (row) => (view === 'all' || row.tone === 'success') && row.event.includes(event.trim()),
    );
  });
  readonly shownRows = computed(() =>
    this.tableState() === 'rows' || this.tableState() === 'refreshing' ? this.visibleRows() : [],
  );
  readonly selectedIndex = computed(() =>
    this.shownRows().findIndex((row) => row.event === this.selected()),
  );
  private readonly toast = inject(ToastService);
  private readonly scope = inject(ViewScope);
  /** 手動排序的示範名單（虛構姓名）；移動時照實際頁面套用 `sortWrites`。 */
  readonly sortPeople = signal([
    { id: 's1', name: '周雅婷', sortOrder: 10 as number | null },
    { id: 's2', name: '吳志明', sortOrder: 20 as number | null },
    { id: 's3', name: '李佳蓉', sortOrder: null as number | null },
    { id: 's4', name: '黃建宏', sortOrder: null as number | null },
    { id: 's5', name: '許怡君', sortOrder: null as number | null },
  ]);
  readonly sortFilter = signal('');
  readonly sortIds = computed(() => this.sortPeople().map((person) => person.id));
  readonly sortName = (id: string) =>
    this.sortPeople().find((person) => person.id === id)?.name ?? '';
  sortMatch(person: { name: string }) {
    return person.name.includes(this.sortFilter().trim());
  }
  sortMove({ id, index }: RowMove) {
    const people = this.sortPeople();
    const writes = sortWrites(people, id, index, 1_000_000);
    this.sortPeople.set(
      applySortWrites(people, writes).sort(
        bySortOrder((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0)),
      ),
    );
  }
  readonly tree = signal<readonly TreeItem[]>([
    {
      id: 'org',
      label: '示範公司',
      icon: 'globe',
      count: 36,
      hasChildren: true,
      children: [
        {
          id: 'rd',
          label: '研發處',
          icon: 'folder',
          count: 24,
          badge: 2,
          badgeLabel: '已選 2 人',
          hasChildren: true,
        },
        { id: 'sales', label: '業務處', icon: 'folder', count: 12, hasChildren: true },
      ],
    },
  ]);
  readonly treeOpen = signal<ReadonlySet<string>>(new Set(['org']));
  readonly treeSelected = signal<string | null>('rd');
  readonly changes: FieldChange[] = [
    { label: '姓名', before: '林可柔', after: '林可蘿' },
    { label: '電子郵件', before: null, after: 'carol@example.test' },
    { label: '分機', before: '2051', after: null },
  ];
  readonly propertyGroups: PropertyGroup[] = [
    {
      id: 'identity',
      label: '身分',
      items: [
        { key: 'displayName', label: '顯示名稱', value: '林可蘿' },
        { key: 'mail', label: '信箱', value: 'carol@example.test' },
        { key: 'employeeID', label: '員工編號', value: null },
      ],
    },
    {
      id: 'account',
      label: '帳號狀態',
      note: '旗標由一個數值拆開；警示放前面。',
      items: [
        {
          key: 'userAccountControl',
          label: '帳號控制',
          value: '66064（0x00010210）',
          badges: [
            { label: '已鎖定', tone: 'danger' },
            { label: '密碼永不過期', tone: 'neutral' },
          ],
        },
        { key: 'lastLogon', label: '最近登入', value: '2026/10/20 09:12:40', hint: '3 天前' },
      ],
    },
    {
      id: 'object',
      label: '物件',
      items: [
        { key: 'memberOf', label: '群組（2）', list: ['研發群組', '專案 A'] },
        { key: 'objectSid', label: 'SID', value: 'S-1-5-21-1000-2000-3000-1101', copy: true },
      ],
    },
  ];
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
  toggleNode(id: string, open: boolean) {
    this.treeOpen.update((ids) => {
      const next = new Set(ids);
      if (open) next.add(id);
      else next.delete(id);
      return next;
    });
    const node = this.findNode(this.tree(), id);
    if (!open || !node || node.children || node.loading) return;
    this.patchNode(id, { loading: true });
    // 以 ViewScope 排程，離開頁面時計時器一併清掉；同一節點只會有一個載入中。
    this.scope.later(
      () => this.patchNode(id, { loading: false, children: LAZY_CHILDREN[id] ?? [] }),
      600,
      `design-tree-${id}`,
    );
  }
  private findNode(items: readonly TreeItem[], id: string): TreeItem | undefined {
    for (const item of items) {
      const found = item.id === id ? item : this.findNode(item.children ?? [], id);
      if (found) return found;
    }
    return undefined;
  }
  private patchNode(id: string, changes: Partial<TreeItem>) {
    const walk = (items: readonly TreeItem[]): TreeItem[] =>
      items.map((item) =>
        item.id === id
          ? { ...item, ...changes }
          : item.children
            ? { ...item, children: walk(item.children) }
            : item,
      );
    this.tree.update(walk);
  }
  /** 模擬重讀一秒後成功。 */
  retry() {
    this.retrying.set(true);
    this.scope.later(
      () => {
        this.retrying.set(false);
        this.tableState.set('rows');
      },
      1000,
      'design-table-retry',
    );
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
