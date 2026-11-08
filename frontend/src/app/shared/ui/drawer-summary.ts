import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
  ViewEncapsulation,
} from '@angular/core';

/**
 * 詳情抽屜的摘要：內容投影的是狀態列（狀態徽章、時間），其下是紀錄名稱與副標。
 * 直接放在 `nx-detail-drawer` 裡；要跟著選取出現時用單層 `@if` 包住，巢狀的控制流程投影不到摘要的位置。
 * 狀態列的 `time` 是投影進來的，所以樣式不封裝，全部掛在 `.ui-drawer-summary` 底下。
 */
@Component({
  selector: 'nx-drawer-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  host: { class: 'ui-drawer-summary' },
  template: `<div class="ui-drawer-status"><ng-content /></div>
    <h3 [class.is-mono]="mono()">{{ heading() }}</h3>
    @if (subtitle()) {
      <p>{{ subtitle() }}</p>
    }`,
  styleUrl: './drawer-summary.scss',
})
export class DrawerSummary {
  readonly heading = input.required<string>();
  readonly subtitle = input<string | null | undefined>();
  /** 名稱是識別字（卡號、檔名、資料庫物件）時用等寬字。 */
  readonly mono = input(false, { transform: booleanAttribute });
}
