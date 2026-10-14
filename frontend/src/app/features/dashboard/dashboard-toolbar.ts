import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { below } from '@shared/browser/breakpoints';
import { mediaQuery } from '@shared/browser/media-query';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { Icon } from '@shared/ui/icon';
import { Select } from '@shared/ui/select';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DashboardStore } from './dashboard-store';

/**
 * The period and currency the overview reads, in one quiet row. Every choice applies itself
 * (typed dates once the viewer pauses), so there is no query button; refresh lives in the header.
 */
@Component({
  selector: 'nx-dashboard-toolbar',
  imports: [DateTimePicker, Icon, Select, ViewSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './dashboard-toolbar.scss',
  host: { role: 'group', 'aria-label': '總覽條件' },
  template: `<nx-view-switch
      label="費用查詢期間"
      appearance="segment"
      [fill]="phone()"
      [options]="store.rangeOptions"
      [value]="store.range()"
      (valueChange)="store.setRange($event)"
    />
    @if (store.range() === 'custom') {
      <div class="toolbar-dates">
        <nx-date-time-picker
          #fromDate
          label="費用開始日期"
          [hideLabel]="true"
          [withTime]="false"
          [value]="store.from()"
          [max]="store.through()"
          (valueChange)="store.setDate('from', $event)"
        />
        <span aria-hidden="true">至</span>
        <nx-date-time-picker
          label="費用結束日期"
          [hideLabel]="true"
          [withTime]="false"
          [value]="store.through()"
          [min]="store.from()"
          [max]="store.today"
          (valueChange)="store.setDate('through', $event)"
        />
      </div>
    } @else {
      <span class="toolbar-period">{{ store.appliedFrom() }} – {{ store.appliedThrough() }}</span>
    }
    <div class="toolbar-end">
      @if (store.units().length) {
        <nx-select
          class="toolbar-unit"
          appearance="pill"
          label="顯示的費用幣別與類型"
          [options]="store.units()"
          [value]="store.unit()"
          (valueChange)="store.unit.set($event)"
        />
      }
      @if (store.scope() === 'platform') {
        <button
          type="button"
          class="toolbar-pill toolbar-button"
          [disabled]="store.exporting() || store.refreshing()"
          (click)="store.export()"
        >
          <nx-icon name="download" />匯出費用
        </button>
      }
    </div>`,
})
export class DashboardToolbar {
  readonly store = inject(DashboardStore);
  /** On a phone the period choices share the full width, as a segmented control does on iOS. */
  readonly phone = mediaQuery(below('phone'));
}
