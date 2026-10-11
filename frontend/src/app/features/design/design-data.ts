import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CodeBlock } from '../../shared/markdown/code-block';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { DataTable } from '../../shared/ui/data-table';
import { DetailDrawer } from '../../shared/ui/detail-drawer';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { StatusBadge } from '../../shared/ui/status-badge';
import { Tabs } from '../../shared/ui/tabs';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { DesignSection } from './design-section';

/** The compact data workspace (filters, table, detail drawer) and the reading components. */
@Component({
  selector: 'nx-design-data',
  imports: [
    CodeBlock,
    DataTable,
    DesignSection,
    DetailDrawer,
    FilterPanel,
    MarkdownView,
    StatusBadge,
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
    .design-data-workspace table {
      min-width: 32rem;
    }
    nx-markdown-view {
      display: block;
      max-width: var(--reading-width);
    }
  `,
  template: `<nx-design-section
      id="design-table"
      class="design-data-workspace ui-density-compact"
      title="資料工作區"
      api="FilterPanel · DataTable · DetailDrawer"
      description="表格獨立捲動、固定表頭；詳情在側欄開啟，可繼續選下一列，窄螢幕改為 modal。"
      [stacked]="true"
    >
      <nx-filter-panel>
        <div class="ui-filter-grid">
          <label class="ui-field"
            >事件名稱<input nxField aria-label="示範事件篩選" placeholder="依事件名稱查詢"
          /></label>
        </div>
      </nx-filter-panel>
      <nx-view-switch
        label="資料範例檢視"
        [options]="dataViews"
        [value]="dataView()"
        (valueChange)="dataView.set($event)"
      />
      <nx-data-table label="共用資料表範例">
        <div table-toolbar class="ui-table-toolbar">
          <h2>事件範例</h2>
          <span class="ui-count-label">{{ visibleRows().length }} 筆</span>
        </div>
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
            @for (entry of visibleRows(); track entry.event) {
              <tr>
                <td>{{ entry.time }}</td>
                <td>
                  <button type="button" class="ui-table-link" (click)="inspect(entry.event)">
                    {{ entry.event }}
                  </button>
                </td>
                <td>
                  <nx-status-badge [tone]="entry.tone">{{ entry.status }}</nx-status-badge>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </nx-data-table>
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
    <nx-detail-drawer title="資料詳情示範" [attr.data-theme]="theme()">
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
  readonly visibleRows = computed(() =>
    this.dataView() === 'all' ? this.rows : this.rows.filter((row) => row.tone === 'success'),
  );
  readonly properties = JSON.stringify(
    { service: 'AiNexus.Host', attempts: 2, state: 'completed' },
    null,
    2,
  );
  readonly markdown =
    '### 好的回答，也需要好的閱讀介面\n\n文字有清楚的層次，資料有可以核對的來源。**段落、表格與程式碼**沿用同一套閱讀元件。\n\n| 項目 | 用途 |\n| --- | --- |\n| 文件來源 | 核對原始資訊 |\n| 成果版本 | 保存每次異動 |\n\n```typescript\nconst workspace = { clarity: true };\n```\n\n> 先讓使用者看得清楚，再讓操作自然發生。';
  private readonly drawer = viewChild.required(DetailDrawer);
  inspect(event: string) {
    this.event.set(event);
    this.drawer().open();
  }
}
