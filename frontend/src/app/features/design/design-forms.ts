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
import { SearchField } from '@shared/ui/search-field';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples } from './design-samples';

/** Text fields, search, dates and in-place editing. */
@Component({
  selector: 'nx-design-forms',
  imports: [DateTimePicker, DesignExample, DesignSection, Field, InlineTitle, SearchField],
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
      api="DateTimePicker"
      description="以台北 wall-clock 輸入與顯示、送出時轉 UTC；民國曆只影響顯示。"
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
    </nx-design-section>`,
})
export class DesignForms {
  readonly samples = inject(DesignSamples);
  private readonly title = viewChild.required(InlineTitle);
  readonly search = signal('');
  readonly bareSearch = signal('');
  readonly calendarDate = signal('2026-10-08T14:30:45');
  readonly optionalDate = signal('');
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
