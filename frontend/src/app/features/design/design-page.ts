import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '../../core/api/api-resource';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { FeaturePage } from '../../core/layout/feature-page';
import { SidebarPanelContent } from '../../core/layout/sidebar-panel';
import { downloadFile } from '../../shared/browser/download';
import { ViewScope } from '../../shared/browser/view-scope';
import { Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { SlidingIndicator } from '../../shared/ui/sliding-indicator';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { DesignCharts } from './design-charts';
import { DesignControls } from './design-controls';
import { DesignData } from './design-data';
import { DesignFeedback } from './design-feedback';
import { DesignForms } from './design-forms';
import { DesignFoundations } from './design-foundations';
import { DesignSamples } from './design-samples';

/** Index of the workbench; ids match the `nx-design-section` ids. */
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
      { id: 'design-flush', name: '扁平對話框' },
      { id: 'design-sheet', name: '底部 sheet' },
      { id: 'design-menus', name: '選單與下拉' },
      { id: 'design-menu-check', name: '核取選單項目' },
      { id: 'design-switches', name: '切換與頁籤' },
      { id: 'design-checkbox', name: '核取方塊' },
    ],
  },
  {
    name: '表單',
    sections: [
      { id: 'design-fields', name: '欄位與搜尋' },
      { id: 'design-search-bare', name: '列內搜尋' },
      { id: 'design-dates', name: '日期與時間' },
      { id: 'design-inline', name: '直接編輯' },
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
      { id: 'design-resources', name: '資源集合' },
      { id: 'design-markdown', name: '內容閱讀' },
    ],
  },
  {
    name: '資料視覺化',
    sections: [
      { id: 'design-ticker', name: '數字與走勢' },
      { id: 'design-donut', name: '環狀分布' },
      { id: 'design-heatmap', name: '日曆熱圖' },
      { id: 'design-spotlight', name: '聚光與傾斜' },
    ],
  },
];
const TOKEN_NAMES = [
  '--canvas',
  '--sidebar',
  '--surface',
  '--surface-subtle',
  '--hover',
  '--line',
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
 * The component workbench: production components with local samples, indexed in the sidebar and
 * previewed in either theme. Nothing here calls a model or writes data.
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
    Icon,
    Notice,
    RouterLink,
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
  /** The page shows only the account's feature names, so the account is its one read. */
  private readonly account = apiResource({ loader: () => this.session.load() });
  readonly loading = this.account.loading;
  readonly error = this.account.error;
  readonly index = INDEX;
  readonly theme = signal<'light' | 'dark'>('light');
  readonly previewThemes = [
    { value: 'light', label: '淺色' },
    { value: 'dark', label: '深色' },
  ];
  /** The section nearest the top of the scroll area, highlighted in the index. */
  readonly current = signal(INDEX[0].sections[0].id);
  readonly preview = viewChild<ElementRef<HTMLElement>>('preview');

  /** A section picked in the index stays current until the reader scrolls by hand. */
  private picked: string | null = null;

  constructor() {
    // The preview renders once the account loads. The current section is the last one whose
    // heading has passed a line near the top of the scroll area; a line further down would pick
    // the next short section right after jumping to the one above it.
    afterRenderEffect((onCleanup) => {
      const preview = this.preview()?.nativeElement;
      const root = preview?.closest<HTMLElement>('.feature-main');
      if (!preview || !root) return;
      let frame = 0;
      const update = () => {
        frame = 0;
        if (this.picked) return;
        const line = root.getBoundingClientRect().top + Math.min(root.clientHeight * 0.25, 96);
        let current = INDEX[0].sections[0].id;
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
