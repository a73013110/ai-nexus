import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RefreshButton } from '@shared/ui/refresh-button';
import { EyeOff, Gauge, GripVertical, LayoutGrid, RotateCcw } from 'lucide';
import { FeaturePage } from '@core/layout/feature-page';
import { ViewScope } from '@shared/browser/view-scope';
import { EXTRA_ICONS, Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { DashboardBoard } from './board/dashboard-board';
import { DashboardEditBar } from './board/dashboard-edit-bar';
import { DashboardLayout } from './board/dashboard-layout';
import { DashboardStore } from './dashboard-store';
import { DashboardToolbar } from './dashboard-toolbar';
import { DASHBOARD_TOUR } from './dashboard-tour';

/**
 * 總覽：一句說明目前狀況的問候語、期間工具列，以及一塊檢視者可以重新排序、調整大小與隱藏的磚塊看板。
 * 資料來自一次讀取（`DashboardStore`）；排列則是瀏覽器偏好（`DashboardLayout`）。
 */
@Component({
  selector: 'nx-dashboard-page',
  imports: [
    RefreshButton,
    DashboardBoard,
    DashboardEditBar,
    DashboardToolbar,
    FeaturePage,
    Icon,
    Notice,
    PendingButton,
    Select,
  ],
  providers: [
    ViewScope,
    DashboardStore,
    DashboardLayout,
    {
      provide: EXTRA_ICONS,
      useValue: {
        grip: GripVertical,
        'eye-off': EyeOff,
        reset: RotateCcw,
        gauge: Gauge,
        layout: LayoutGrid,
      },
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  readonly store = inject(DashboardStore);
  readonly layout = inject(DashboardLayout);
  readonly session = this.store.session;
  readonly tour = DASHBOARD_TOUR;
  readonly scopes = [
    { value: 'personal', label: '我的工作' },
    { value: 'platform', label: '整個平台' },
  ];
  /** 標題下方的一行：問候語，接著是進行中的工作或需要注意的事。 */
  readonly summary = computed(() => {
    const hour = new Date().getHours();
    const part =
      hour < 5 ? '夜深了' : hour < 11 ? '早安' : hour < 14 ? '午安' : hour < 18 ? '下午好' : '晚安';
    const name = this.session.me()?.displayName;
    const greeting = name ? `${part}，${name}。` : `${part}。`;
    if (!this.store.data()) return greeting;
    const active = this.store.activeWork(),
      attention = this.store.attention();
    if (active) return `${greeting}目前有 ${active} 件工作處理中。`;
    if (attention.length)
      return `${greeting}${attention.map((item) => item.title).join('、')}需要留意。`;
    return `${greeting}一切就緒，沒有進行中的工作。`;
  });
}
