import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { SidebarPanelContent } from '@core/layout/sidebar-panel';
import { downloadFile } from '@shared/browser/download';
import { ViewScope } from '@shared/browser/view-scope';
import { Notice } from '@shared/ui/notice';
import { SlidingIndicator } from '@shared/ui/sliding-indicator';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DesignCharts } from './sections/design-charts';
import { DesignControls } from './sections/design-controls';
import { DesignData } from './sections/design-data';
import { DesignFeedback } from './sections/design-feedback';
import { DesignForms } from './sections/design-forms';
import { DesignFoundations } from './sections/design-foundations';
import { DesignSamples } from './sections/design-samples';

/** 工作台的索引；id 與 `nx-design-section` 的 id 相同。 */
const INDEX = [
  {
    name: '基礎',
    sections: [
      { id: 'design-colors', name: '色彩' },
      { id: 'design-type', name: '字級' },
      { id: 'design-space', name: '間距與圓角' },
      { id: 'design-motion', name: '動效' },
    ],
  },
  {
    name: '操作與選取',
    sections: [
      { id: 'design-buttons', name: '按鈕' },
      { id: 'design-dialogs', name: '對話框' },
      { id: 'design-dialog', name: '對話框尺寸' },
      { id: 'design-save-bar', name: '儲存列' },
      { id: 'design-menus', name: '選單與下拉' },
      { id: 'design-menu-check', name: '核取選單項目' },
      { id: 'design-switches', name: '切換與頁籤' },
      { id: 'design-floating-toolbar', name: '浮動工具列' },
      { id: 'design-checkbox', name: '核取方塊' },
      { id: 'design-choice-groups', name: '分組多選' },
    ],
  },
  {
    name: '表單',
    sections: [
      { id: 'design-fields', name: '欄位與搜尋' },
      { id: 'design-search-bare', name: '列內搜尋' },
      { id: 'design-person-picker', name: '人員選擇器' },
      { id: 'design-person-browser', name: '從名單挑人' },
      { id: 'design-tree-select', name: '階層選擇器' },
      { id: 'design-form-sections', name: '表單段落' },
      { id: 'design-dates', name: '日期與時間' },
      { id: 'design-inline', name: '直接編輯' },
      { id: 'design-settings', name: '偏好設定列' },
    ],
  },
  {
    name: '回饋',
    sections: [
      { id: 'design-notices', name: '提示' },
      { id: 'design-toasts', name: '通知' },
      { id: 'design-island', name: '動態島' },
      { id: 'design-badges', name: '狀態與數量' },
      { id: 'design-progress', name: '進度與生成' },
      { id: 'design-popover', name: '資訊浮層與空狀態' },
    ],
  },
  {
    name: '資料與內容',
    sections: [
      { id: 'design-table', name: '資料工作區' },
      { id: 'design-pagination', name: '分頁' },
      { id: 'design-resources', name: '資源集合' },
      { id: 'design-tree', name: '樹狀瀏覽' },
      { id: 'design-changes', name: '欄位變更' },
      { id: 'design-properties', name: '屬性清單' },
      { id: 'design-markdown', name: '內容閱讀' },
    ],
  },
  {
    name: '資料視覺化',
    sections: [
      { id: 'design-ticker', name: '數字與走勢' },
      { id: 'design-stat', name: '頭條數字' },
      { id: 'design-donut', name: '環狀分布' },
      { id: 'design-heatmap', name: '日曆熱圖' },
      { id: 'design-calendar', name: '月曆' },
      { id: 'design-spotlight', name: '聚光與傾斜' },
    ],
  },
];
const FIRST_SECTION = INDEX[0].sections[0].id;
const TOKEN_NAMES = [
  '--canvas',
  '--sidebar',
  '--surface',
  '--surface-subtle',
  '--hover',
  '--line',
  '--control-line',
  '--ink',
  '--secondary',
  '--muted',
  '--accent',
  '--accent-text',
  '--signal',
  '--activity',
  '--activity-rest',
  '--series-1',
  '--series-2',
  '--heat',
  '--warning',
  '--error',
  '--danger-surface',
  '--danger-text',
  '--focus',
  '--text-body',
  '--text-ui',
  '--text-caption',
  '--button-target',
  '--motion',
  '--ease-spring',
  '--shadow',
  '--shadow-floating',
  '--dialog-radius',
];

/**
 * 元件工作台：正式環境的元件搭配本地範例，列在側邊欄索引中，並在淺色或深色主題下預覽。
 * 這裡不會呼叫模型，也不寫入資料。
 */
@Component({
  selector: 'nx-design-page',
  styleUrl: './design-page.scss',
  imports: [
    DesignCharts,
    DesignControls,
    DesignData,
    DesignFeedback,
    DesignForms,
    DesignFoundations,
    FeaturePage,
    Notice,
    SidebarPanelContent,
    SlidingIndicator,
    ViewSwitch,
  ],
  providers: [ViewScope, DesignSamples],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './design-page.html',
})
export class DesignPage {
  readonly session = inject(WorkspaceSession);
  /** 頁面只顯示帳號的功能名稱，所以帳號是它唯一的讀取。 */
  private readonly account = apiResource({ loader: () => this.session.load() });
  readonly loading = this.account.loading;
  readonly error = this.account.error;
  readonly index = INDEX;
  readonly theme = signal<'light' | 'dark'>('light');
  readonly previewThemes = [
    { value: 'light', label: '淺色' },
    { value: 'dark', label: '深色' },
  ];
  /** 捲動區域中最靠近頂部的區段，在索引中標示為目前位置。 */
  readonly current = signal(FIRST_SECTION);
  readonly preview = viewChild<ElementRef<HTMLElement>>('preview');

  /** 從索引選取的區段會保持為目前位置，直到讀者自己捲動。 */
  private picked: string | null = null;

  constructor() {
    // 帳號載入後才渲染預覽。目前區段是標題已越過捲動區域頂部附近某一線的最後一個；
    // 若線設得更低，跳到前面區段之後，會立刻選到緊接其後的短區段。
    afterRenderEffect((onCleanup) => {
      const preview = this.preview()?.nativeElement;
      const root = preview?.closest<HTMLElement>('.feature-main');
      if (!preview || !root) return;
      let frame = 0;
      const update = () => {
        frame = 0;
        if (this.picked) return;
        const line = root.getBoundingClientRect().top + Math.min(root.clientHeight * 0.25, 96);
        let current = FIRST_SECTION;
        for (const section of preview.querySelectorAll('nx-design-section'))
          if (section.getBoundingClientRect().top <= line) current = section.id;
        this.current.set(current);
      };
      const schedule = () => (frame ||= requestAnimationFrame(update));
      const release = () => (this.picked = null);
      const manual = ['wheel', 'touchstart', 'pointerdown', 'keydown'] as const;
      root.addEventListener('scroll', schedule, { passive: true });
      for (const type of manual) root.addEventListener(type, release, { passive: true });
      update();
      onCleanup(() => {
        cancelAnimationFrame(frame);
        root.removeEventListener('scroll', schedule);
        for (const type of manual) root.removeEventListener(type, release);
      });
    });
  }
  load() {
    this.account.reload();
  }
  go(event: Event, id: string) {
    event.preventDefault();
    const section = document.getElementById(id);
    if (!section) return;
    this.picked = id;
    this.current.set(id);
    section.scrollIntoView({ block: 'start' });
    section.querySelector<HTMLElement>('h3')?.focus({ preventScroll: true });
  }
  exportTokens() {
    const preview = this.preview()?.nativeElement;
    if (!preview) return;
    const style = getComputedStyle(preview);
    const tokens = Object.fromEntries(
      TOKEN_NAMES.map((name) => [name, style.getPropertyValue(name).trim()]),
    );
    downloadFile(
      JSON.stringify({ theme: this.theme(), tokens }, null, 2),
      `ai-nexus-tokens-${this.theme()}`,
      'json',
    );
  }
}
