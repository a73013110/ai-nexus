import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import {
  AudioWaveform,
  EyeOff,
  Gauge,
  GripVertical,
  LayoutGrid,
  Orbit,
  RotateCcw,
  Sparkles,
} from 'lucide';
import { FeaturePage } from '../../core/layout/feature-page';
import { ViewScope } from '../../shared/browser/view-scope';
import { DateTimePicker } from '../../shared/ui/date-time-picker';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { EXTRA_ICONS, Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { Select } from '../../shared/ui/select';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { PriceBook } from '../billing/price-book';
import { DashboardBoard } from './dashboard-board';
import { DashboardEditBar } from './dashboard-edit-bar';
import { DASHBOARD_STYLES, DashboardLayout, type DashboardStyle } from './dashboard-layout';
import { DashboardStore } from './dashboard-store';
import { AuroraHero } from './heroes/aurora-hero';
import { OrbitHero } from './heroes/orbit-hero';
import { PulseHero } from './heroes/pulse-hero';

/** How far tiles lean toward the pointer in each style, in degrees. */
const TILT: Record<DashboardStyle, number> = { aurora: 3, orbit: 5, pulse: 0 };

/**
 * The overview: a hero in the chosen style over a board of tiles the viewer can reorder, resize
 * and hide. Data comes from one read (`DashboardStore`); arrangement is a browser preference
 * (`DashboardLayout`).
 */
@Component({
  selector: 'nx-dashboard-page',
  imports: [
    AuroraHero,
    DashboardBoard,
    DashboardEditBar,
    DateTimePicker,
    FeaturePage,
    FilterPanel,
    Icon,
    Notice,
    OrbitHero,
    PriceBook,
    PulseHero,
    Select,
    ViewSwitch,
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
        sparkles: Sparkles,
        orbit: Orbit,
        waves: AudioWaveform,
        reset: RotateCcw,
        gauge: Gauge,
        layout: LayoutGrid,
      },
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-dashboard-style]': 'layout.style()',
    // The orbit style is a night sky whatever the theme.
    '[attr.data-theme]': "layout.style() === 'orbit' ? 'dark' : null",
  },
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  readonly store = inject(DashboardStore);
  readonly layout = inject(DashboardLayout);
  readonly session = this.store.session;
  readonly styles = DASHBOARD_STYLES;
  readonly scopes = [
    { value: 'personal', label: '我的工作' },
    { value: 'platform', label: '整個平台' },
  ];
  readonly tilt = computed(() => TILT[this.layout.style()]);
  setStyle(value: string) {
    const style = DASHBOARD_STYLES.find((x) => x.value === value)?.value;
    if (style) this.layout.setStyle(style);
  }
}
