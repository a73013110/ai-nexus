import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { EyeOff, Gauge, GripVertical, LayoutGrid, RotateCcw } from 'lucide';
import { FeaturePage } from '../../core/layout/feature-page';
import { ViewScope } from '../../shared/browser/view-scope';
import { EXTRA_ICONS, Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { Select } from '../../shared/ui/select';
import { PriceBook } from '../billing/price-book';
import { DashboardBoard } from './dashboard-board';
import { DashboardEditBar } from './dashboard-edit-bar';
import { DashboardLayout } from './dashboard-layout';
import { DashboardStore } from './dashboard-store';
import { DashboardToolbar } from './dashboard-toolbar';

/**
 * The overview: a greeting that says what is happening now, the period toolbar and a board of
 * tiles the viewer can reorder, resize and hide. Data comes from one read (`DashboardStore`);
 * arrangement is a browser preference (`DashboardLayout`).
 */
@Component({
  selector: 'nx-dashboard-page',
  imports: [
    DashboardBoard,
    DashboardEditBar,
    DashboardToolbar,
    FeaturePage,
    Icon,
    Notice,
    PriceBook,
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
  readonly scopes = [
    { value: 'personal', label: '我的工作' },
    { value: 'platform', label: '整個平台' },
  ];
  /** The line under the title: a greeting, then work in progress or what needs attention. */
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
    if (attention.length) return `${greeting}${attention.map((x) => x.title).join('、')}需要留意。`;
    return `${greeting}一切就緒，沒有進行中的工作。`;
  });
}
