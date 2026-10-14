import type { Type } from '@angular/core';
import type { DashboardSectionDto } from '../../core/api/schema';
import { sectionIcon } from './dashboard-format';
import { ActivityWidget } from './widgets/activity-widget';
import { FlowWidget } from './widgets/flow-widget';
import { MixWidget } from './widgets/mix-widget';
import { OverviewWidget } from './widgets/overview-widget';
import { RecentWidget } from './widgets/recent-widget';
import { SectionWidget } from './widgets/section-widget';
import { SpendWidget } from './widgets/spend-widget';
import { TokensWidget } from './widgets/tokens-widget';
import { UsersWidget } from './widgets/users-widget';

/** Column spans on the 12-column board: small 3, medium 4, large 6, wide 8, full 12. */
export type TileSize = 's' | 'm' | 'l' | 'w' | 'xl';
export const TILE_SIZES: readonly { value: TileSize; label: string }[] = [
  { value: 's', label: '小' },
  { value: 'm', label: '中' },
  { value: 'l', label: '大' },
  { value: 'w', label: '寬' },
  { value: 'xl', label: '滿版' },
];
export function isTileSize(value: unknown): value is TileSize {
  return TILE_SIZES.some((x) => x.value === value);
}

/** One kind of tile the overview can show. */
export interface DashboardWidget {
  id: string;
  title: string;
  /** What the tile shows, in the customize list. */
  description: string;
  icon: string;
  size: TileSize;
  tall?: boolean;
  component: Type<unknown>;
  inputs?: Record<string, unknown>;
}

export interface WidgetContext {
  scope: 'personal' | 'platform';
  sections: readonly DashboardSectionDto[];
}

/**
 * Default order, the same for everyone; the user table only exists in the platform scope. Section
 * tiles of new features follow in the order the backend sends them.
 */
const ORDER: readonly string[] = [
  'overview',
  'spend',
  'activity',
  'users',
  'section:chat',
  'section:knowledge',
  'section:tasks',
  'flow',
  'recent',
  'mix',
  'section:notifications',
  'tokens',
  'section:web-search',
  'section:files',
  'section:projects',
  'section:artifacts',
  'section:shared',
  'section:quality',
  'section:repositories',
];

/** Default sizes chosen so the default order fills whole rows. */
const SECTION_SIZES: Record<string, TileSize> = {
  files: 's',
  projects: 's',
  artifacts: 's',
  shared: 's',
  quality: 'l',
  repositories: 'l',
  'web-search': 'l',
};

/**
 * Every tile available for the current read, in default order. Composite tiles combine several
 * sections; each section also has its own tile, so a new backend section shows up automatically.
 */
export function dashboardWidgets(context: WidgetContext): DashboardWidget[] {
  const widgets: DashboardWidget[] = [
    {
      id: 'overview',
      title: '關鍵指標',
      description: '已知費用、模型與工具呼叫、Token 與處理中工作',
      icon: 'gauge',
      size: 'xl',
      component: OverviewWidget,
    },
    {
      id: 'spend',
      title: '費用趨勢',
      description: '每日已知費用，可切換幣別與費用類型',
      icon: 'money',
      size: 'w',
      component: SpendWidget,
    },
    {
      id: 'activity',
      title: '呼叫熱度',
      description: '每日模型與工具呼叫的日曆熱圖',
      icon: 'calendar',
      size: 'm',
      component: ActivityWidget,
    },
    {
      id: 'flow',
      title: '工作流程',
      description: '從原檔、索引到回覆、成果與分享的流向',
      icon: 'integrations',
      size: 'xl',
      component: FlowWidget,
    },
    {
      id: 'recent',
      title: '繼續工作',
      description: '各功能中自己最近的對話、文件與任務',
      icon: 'history',
      size: 'm',
      component: RecentWidget,
    },
    {
      id: 'mix',
      title: '模型分布',
      description: '費用或 Token 在各模型與工具之間的占比',
      icon: 'chart',
      size: 'm',
      component: MixWidget,
    },
    {
      id: 'tokens',
      title: 'Token 使用量',
      description: '依模型與輸入／輸出檢視每日 Token',
      icon: 'database',
      size: 'l',
      component: TokensWidget,
    },
    ...context.sections.map((section): DashboardWidget => ({
      id: 'section:' + section.id,
      title: section.title,
      description: section.metrics
        .slice(0, 3)
        .map((x) => x.label)
        .join('、'),
      icon: sectionIcon(section.id),
      size: SECTION_SIZES[section.id] ?? 'm',
      component: SectionWidget,
      inputs: { sectionId: section.id },
    })),
  ];
  if (context.scope === 'platform')
    widgets.push({
      id: 'users',
      title: '使用者區間費用',
      description: '依使用者排序的呼叫與已知費用，可檢視單一使用者',
      icon: 'user',
      size: 'xl',
      component: UsersWidget,
    });
  const rank = (id: string) => {
    const index = ORDER.indexOf(id);
    return index < 0 ? ORDER.length : index;
  };
  // Array sort is stable, so unknown sections keep the backend's order after the known ones.
  return widgets.sort((a, b) => rank(a.id) - rank(b.id));
}
