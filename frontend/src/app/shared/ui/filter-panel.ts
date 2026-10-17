import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Icon } from './icon';

/** 查詢狀態與送出仍留在功能自己的原生表單中。 */
@Component({
  selector: 'nx-filter-panel',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './filter-panel.scss',
  host: { class: 'ui-filter-panel' },
  template: `<div class="ui-filter-heading">
      <strong><nx-icon name="filter" />篩選</strong>
      <div class="page-actions"><ng-content select="[filter-actions]" /></div>
    </div>
    <ng-content />`,
})
export class FilterPanel {}
