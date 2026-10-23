import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { below } from '@shared/browser/breakpoints';
import { mediaQuery } from '@shared/browser/media-query';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { Icon } from '@shared/ui/icon';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DashboardStore } from './dashboard-store';

/**
 * 總覽讀取的期間與幣別，放在一列安靜的工具列中。每個選擇都會自行套用
 * （輸入的日期在檢視者停止輸入後套用），因此沒有查詢按鈕；重新整理在標題列。
 */
@Component({
  selector: 'nx-dashboard-toolbar',
  imports: [DateTimePicker, Icon, PendingButton, Select, ViewSwitch],
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
      <!-- 幣別來自讀取結果：首次載入時先佔位並轉圈，不在資料到了才冒出來。 -->
      @if (store.units().length || firstLoad()) {
        <nx-select
          class="toolbar-unit"
          appearance="pill"
          label="顯示的費用幣別與類型"
          [options]="store.units()"
          [loading]="firstLoad()"
          [value]="store.unit()"
          (valueChange)="store.unit.set($event)"
        />
      }
      @if (store.scope() === 'platform') {
        <button
          nxPending
          type="button"
          class="toolbar-pill toolbar-button"
          [pending]="store.exporting()"
          [disabled]="store.refreshing() && !store.exporting()"
          (click)="store.export()"
        >
          <nx-icon name="download" />{{ store.exporting() ? '匯出中…' : '匯出費用' }}
        </button>
      }
    </div>`,
})
export class DashboardToolbar {
  readonly store = inject(DashboardStore);
  /** 在手機上期間選項平分整個寬度，就像 iOS 的分段控制一樣。 */
  readonly phone = mediaQuery(below('phone'));
  protected readonly firstLoad = computed(() => this.store.loading() && !this.store.data());
}
