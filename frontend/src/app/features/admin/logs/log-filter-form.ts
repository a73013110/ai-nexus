import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { APP_TIME_ZONE } from '../../../shared/browser/format';
import { Field } from '../../../shared/ui/field';
import { FilterPanel } from '../../../shared/ui/filter-panel';
import { DateTimePicker } from '../../../shared/ui/date-time-picker';
import { Icon } from '../../../shared/ui/icon';
import { Select, type SelectOption } from '../../../shared/ui/select';
import { LogFilterState, type LogFields } from './log-filter-state';

const levels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];

/** Time range, issue code, level and advanced fields of the log query. */
@Component({
  selector: 'nx-log-filter-form',
  imports: [FormsModule, Field, FilterPanel, DateTimePicker, Icon, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './log-filter-form.scss',
  templateUrl: './log-filter-form.html',
})
export class LogFilterForm {
  readonly state = inject(LogFilterState);
  readonly busy = input(false);
  readonly submitted = output<void>();
  readonly timezone = APP_TIME_ZONE;
  readonly levelOptions: SelectOption[] = [
    { value: '', label: '全部等級' },
    ...levels.map((value) => ({ value, label: value })),
  ];
  readonly ranges: SelectOption[] = [
    { value: '.25', label: '最近 15 分鐘' },
    { value: '1', label: '最近 1 小時' },
    { value: '24', label: '最近 24 小時' },
    { value: '168', label: '最近 7 天' },
    { value: 'custom', label: '自訂時間' },
  ];
  readonly advancedFields: { key: keyof LogFields; label: string; maxLength: number }[] = [
    { key: 'category', label: '模組 Category', maxLength: 180 },
    { key: 'eventName', label: '事件名稱', maxLength: 100 },
    { key: 'traceId', label: 'TraceId', maxLength: 32 },
    { key: 'jobId', label: 'JobId', maxLength: 36 },
    { key: 'runId', label: 'RunId', maxLength: 36 },
    { key: 'errorCode', label: '錯誤碼', maxLength: 80 },
    { key: 'instance', label: '執行個體', maxLength: 100 },
    { key: 'text', label: '訊息模板文字（限一天）', maxLength: 72 },
  ];
  reset() {
    this.state.reset();
    this.submitted.emit();
  }
}
