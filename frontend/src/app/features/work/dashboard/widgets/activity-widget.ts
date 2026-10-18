import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { HeatmapCalendar } from '@shared/ui/heatmap-calendar';
import { DashboardStore } from '../dashboard-store';

/** 期間內哪些日子忙碌，依模型與工具呼叫計算。 */
@Component({
  selector: 'nx-activity-widget',
  imports: [HeatmapCalendar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `<nx-heatmap-calendar
    label="每日模型與工具呼叫"
    unit=" 次"
    [points]="store.callsDaily()"
    [from]="store.appliedFrom()"
    [through]="store.appliedThrough()"
  />`,
})
export class ActivityWidget {
  readonly store = inject(DashboardStore);
}
