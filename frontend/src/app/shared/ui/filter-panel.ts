import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Icon } from './icon';

/** Query state and submission stay in the feature's native form. */
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
