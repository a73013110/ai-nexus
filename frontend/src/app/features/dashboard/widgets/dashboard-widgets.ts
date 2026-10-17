import type { Type } from '@angular/core';
import type { DashboardSectionDto } from '@core/api/schema';
import { sectionIcon } from '../dashboard-format';
import { ActivityWidget } from './activity-widget';
import { FlowWidget } from './flow-widget';
import { MixWidget } from './mix-widget';
import { OverviewWidget } from './overview-widget';
import { RecentWidget } from './recent-widget';
import { SectionWidget } from './section-widget';
import { SpendWidget } from './spend-widget';
import { TokensWidget } from './tokens-widget';
import { UsersWidget } from './users-widget';

/** 12 欄看板上的欄跨度：小 3、中 4、大 6、寬 8、全寬 12。 */
export type TileSize = 's' | 'm' | 'l' | 'w' | 'xl';
export const TILE_SIZES: readonly { value: TileSize; label: string }[] = [
  { value: 's', label: '小' },
  { value: 'm', label: '中' },
  { value: 'l', label: '大' },
  { value: 'w', label: '寬' },
  { value: 'xl', label: '滿版' },
];
export function isTileSize(value: unknown): value is TileSize {
  return TILE_SIZES.some((item) => item.value === value);
}

/** 總覽可以顯示的一種磚塊。 */
export interface DashboardWidget {
  id: string;
  title: string;
  /** 磚塊顯示的內容，於自訂列表中呈現。 */
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
 * 預設順序，所有人相同；使用者表格只存在於平台範圍。新功能的區段磚塊依後端送來的順序排在其後。
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

/** 預設大小的選擇，讓預設順序剛好填滿整列。 */
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
 * 目前讀取可用的所有磚塊，依預設順序排列。組合磚塊會合併多個區段；
 * 每個區段也有自己的磚塊，因此後端新增區段時會自動出現。
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
        .map((item) => item.label)
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
  // 陣列排序是穩定的，所以未知區段會排在已知區段之後，並保留後端的順序。
  return widgets.sort((first, second) => rank(first.id) - rank(second.id));
}
