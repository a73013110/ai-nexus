import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  linkedSignal,
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
import { queryField } from '@shared/ui/query-fields';
import { queryState } from '@shared/ui/query-state';
import { SlidingIndicator } from '@shared/ui/sliding-indicator';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DesignCharts } from './sections/design-charts';
import { DesignControls } from './sections/design-controls';
import { DesignData } from './sections/design-data';
import { DesignFeedback } from './sections/design-feedback';
import { DesignForms } from './sections/design-forms';
import { DesignFoundations } from './sections/design-foundations';
import { DesignIndex, type DesignIndexEntry } from './sections/design-index';
import { DesignSamples } from './sections/design-samples';

/**
 * 範例分類：頁籤的順序與網址的 `?group=`。第一個是預設值，不寫進網址。
 * 新增分類時在這裡加一筆，並在 design-page.html 的 `@switch` 加對應的 `@case`。
 */
const GROUPS = [
  { value: 'foundations', label: '基礎' },
  { value: 'controls', label: '操作與選取' },
  { value: 'forms', label: '表單' },
  { value: 'feedback', label: '回饋' },
  { value: 'data', label: '資料與內容' },
  { value: 'charts', label: '資料視覺化' },
] as const satisfies readonly TabItem[];
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
 * 元件工作台：正式環境的元件搭配本地範例，依分類分頁、在淺色或深色主題下預覽；
 * 側欄索引列出目前分類的範例。這裡不會呼叫模型，也不寫入資料。
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
    Tabs,
    ViewMotion,
    ViewSwitch,
  ],
  // DesignSamples 在頁面層級：切換分類時各分類元件會重建，共用的示範狀態仍保留。
  providers: [ViewScope, DesignSamples, DesignIndex],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './design-page.html',
})
export class DesignPage {
  readonly session = inject(WorkspaceSession);
  /** 頁面只顯示帳號的功能名稱，所以帳號是它唯一的讀取。 */
  private readonly account = apiResource({ loader: () => this.session.load() });
  readonly loading = this.account.loading;
  readonly error = this.account.error;
  readonly groups = GROUPS;
  /** 目前分類放在網址，重新整理與分享連結都停在同一類。 */
  private readonly query = queryState({ fields: { group: queryField.choice('group', GROUPS) } });
  readonly group = computed(() => this.query.applied().group);
  readonly groupLabel = computed(() => GROUPS.find((item) => item.value === this.group())!.label);
  readonly sections = inject(DesignIndex).sections;
  readonly theme = signal<'light' | 'dark'>('light');
  readonly previewThemes = [
    { value: 'light', label: '淺色' },
    { value: 'dark', label: '深色' },
  ];
  /** 捲動區域中最靠近頂部的區段，在索引中標示為目前位置；換分類時回到第一個。 */
  readonly current = linkedSignal(() => this.sections()[0]?.id ?? '');
  readonly preview = viewChild<ElementRef<HTMLElement>>('preview');

  /** 從索引選取的區段會保持為目前位置，直到讀者自己捲動。 */
  private picked: string | null = null;

  constructor() {
    // 目前區段是標題已越過捲動區域頂部附近某一線的最後一個；
    // 若線設得更低，跳到前面區段之後，會立刻選到緊接其後的短區段。
    afterRenderEffect((onCleanup) => {
      const sections = this.sections();
      const root = this.scrollRoot();
      if (!root || !sections.length) return;
      let frame = 0;
      const update = () => {
        frame = 0;
        if (this.picked) return;
        const line = root.getBoundingClientRect().top + Math.min(root.clientHeight * 0.25, 96);
        let current = sections[0].id;
        for (const section of sections)
          if (section.element.getBoundingClientRect().top <= line) current = section.id;
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
  /** 換分類後從頂端讀起；留在原本的捲動位置會落在新分類的中段。 */
  select(value: string) {
    const group = GROUPS.find((item) => item.value === value)?.value;
    if (!group || group === this.group()) return;
    this.picked = null;
    this.query.commit({ group });
    this.scrollRoot()?.scrollTo({ top: 0 });
  }
  go(event: Event, section: DesignIndexEntry) {
    event.preventDefault();
    this.picked = section.id;
    this.current.set(section.id);
    section.element.scrollIntoView({ block: 'start' });
    section.element.querySelector<HTMLElement>('h3')?.focus({ preventScroll: true });
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
  /** 頁面的捲動區域；帳號載入、預覽渲染前不存在。 */
  private scrollRoot() {
    return this.preview()?.nativeElement.closest<HTMLElement>('.feature-main') ?? null;
  }
}
