import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { Field } from '@shared/ui/field';
import { InlineTitle } from '@shared/ui/inline-title';
import { KeyCombo } from '@shared/ui/key-combo';
import { SearchField } from '@shared/ui/search-field';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { TimeRangePicker, hoursAgo, type TimeRange } from '@shared/ui/time-range-picker';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples } from './design-samples';

/** 文字欄位、搜尋、日期與原地編輯。 */
@Component({
  selector: 'nx-design-forms',
  imports: [
    DateTimePicker,
    DesignExample,
    DesignSection,
    Field,
    InlineTitle,
    KeyCombo,
    SearchField,
    SettingGroup,
    SettingRow,
    TimeRangePicker,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    nx-design-example {
      flex: 1 1 14rem;
    }
    nx-design-example > * {
      width: 100%;
    }
    .design-settings {
      width: min(100%, 40rem);
    }
    .design-bar-frame {
      width: min(100%, 28rem);
      border: 1px solid var(--line);
      border-radius: var(--dialog-radius);
    }
    .design-bar-frame p {
      margin: 0;
      padding: var(--p-space-3) var(--p-space-4);
      color: var(--secondary);
      font-size: var(--text-caption);
    }
  `,
  template: `<nx-design-section
      id="design-fields"
      title="欄位與搜尋"
      api="nxField · SearchField"
      description="驗證說明靠近欄位；手機輸入字級至少 16px，避免 iOS 自動放大。"
    >
      <nx-design-example label="nxField">
        <label class="ui-field"
          >共用表單欄位<input nxField aria-label="示範資料篩選" placeholder="依事件名稱查詢"
        /></label>
      </nx-design-example>
      <nx-design-example label="SearchField">
        <nx-search-field
          label="示範搜尋"
          placeholder="搜尋文件與對話"
          [value]="search()"
          (valueChange)="search.set($event)"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-search-bare"
      title="列內搜尋"
      api='SearchField appearance="bare"'
      description="放進已有外框的列（對話框標題列、面板頂端）時去掉欄位外框，由列的細線與游標標示輸入位置。"
    >
      <div class="design-bar-frame">
        <div class="dialog-bar">
          <nx-search-field
            appearance="bare"
            label="示範列內搜尋"
            placeholder="搜尋檔案庫中的檔案"
            [value]="bareSearch()"
            (valueChange)="bareSearch.set($event)"
          />
        </div>
        <p>{{ bareSearch() ? '搜尋：' + bareSearch() : '結果列在標題列下方。' }}</p>
      </div>
    </nx-design-section>
    <nx-design-section
      id="design-dates"
      title="日期與時間"
      api="DateTimePicker · TimeRangePicker"
      description="以台北 wall-clock 輸入與顯示、送出時轉 UTC；民國曆只影響顯示。查詢的時間範圍用 TimeRangePicker：預設區間與自訂起訖在同一個浮層，時區只寫一次。"
    >
      <nx-design-example label="西元">
        <nx-date-time-picker
          label="西元日期時間"
          [value]="calendarDate()"
          (valueChange)="calendarDate.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="民國">
        <nx-date-time-picker
          label="民國日期時間"
          calendarSystem="roc"
          [value]="calendarDate()"
          (valueChange)="calendarDate.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="選填、只有日期">
        <nx-date-time-picker
          label="選填日期"
          [withTime]="false"
          [required]="false"
          [value]="optionalDate()"
          (valueChange)="optionalDate.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="時間範圍">
        <nx-time-range-picker
          label="示範時間範圍"
          [presets]="ranges"
          [value]="range()"
          (valueChange)="range.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="時間範圍、只有日期、可不限">
        <nx-time-range-picker
          label="示範日期範圍"
          [presets]="dayRanges"
          [withTime]="false"
          [required]="false"
          [value]="dayRange()"
          (valueChange)="dayRange.set($event)"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-inline"
      title="直接編輯"
      api="InlineTitle"
      description="雙擊標題或按 F2／Enter 編輯；Enter 儲存、Esc 取消。"
    >
      <nx-inline-title
        appearance="block"
        [value]="samples.title()"
        [scope]="'design-' + samples.titleVersion()"
        [save]="saveTitle"
      />
    </nx-design-section>
    <nx-design-section
      id="design-settings"
      title="偏好設定列"
      api="SettingGroup · SettingRow · KeyCombo"
      description="個人偏好一列一項：名稱與一句說明在左、控制項在右。給 options 是下拉選單、給 checked 是開關，其他控制項直接投影；窄卡片時控制項靠上對齊。按鍵用 KeyCombo，依平台顯示 ⌘ 或 Ctrl。"
    >
      <nx-setting-group
        class="design-settings"
        heading="示範偏好"
        description="卡片標題下最多一句說明。"
      >
        <nx-setting-row
          label="主題"
          description="下拉選單的名稱取自列名稱。"
          [options]="themes"
          [value]="theme()"
          (valueChange)="theme.set($event)"
        />
        <nx-setting-row
          label="減少動態效果"
          description="開關的名稱與說明由列提供。"
          [checked]="reduced()"
          (checkedChange)="reduced.set($event)"
        />
        <nx-setting-row
          label="停用的開關"
          description="需要先取得權限。"
          [checked]="false"
          [disabled]="true"
        >
          <span setting-detail>說明下方可投影狀態原因。</span>
        </nx-setting-row>
        <nx-setting-row label="快捷指令" description="投影其他控制項。">
          <nx-key-combo keys="mod+k" />
        </nx-setting-row>
      </nx-setting-group>
    </nx-design-section>`,
})
export class DesignForms {
  readonly samples = inject(DesignSamples);
  private readonly title = viewChild.required(InlineTitle);
  readonly search = signal('');
  readonly bareSearch = signal('');
  readonly calendarDate = signal('2026-10-08T14:30:45');
  readonly optionalDate = signal('');
  readonly ranges = [
    { value: '1h', label: '最近 1 小時', duration: hoursAgo(1) },
    { value: '24h', label: '最近 24 小時', duration: hoursAgo(24) },
  ];
  readonly range = signal<TimeRange>({ preset: '24h', from: '', to: '' });
  readonly dayRanges = [
    { value: 'all', label: '不限時間' },
    { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
  ];
  readonly dayRange = signal<TimeRange>({ preset: 'all', from: '', to: '' });
  readonly themes = [
    { value: 'system', label: '跟隨系統' },
    { value: 'dark', label: '深色' },
  ];
  readonly theme = signal('system');
  readonly reduced = signal(false);
  readonly saveTitle = async (value: string) => {
    this.samples.title.set(value);
    return true;
  };
  constructor() {
    effect(() => {
      if (this.samples.editRequest()) untracked(() => this.title().begin());
    });
  }
}
