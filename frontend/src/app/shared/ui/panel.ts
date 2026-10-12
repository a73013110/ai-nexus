import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';

let sequence = 0;

/**
 * A titled resource surface in the overview's tile skin: a quiet heading with an optional icon and
 * count, actions at the right, then the feature's content. Its region is named by the heading.
 */
@Component({
  selector: 'nx-panel',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './panel.scss',
  host: { role: 'region', '[attr.aria-labelledby]': 'id' },
  template: `<div class="panel-head">
      @if (icon()) {
        <nx-icon class="panel-icon" [name]="icon()" />
      }
      <h2 [id]="id">{{ heading() }}</h2>
      @if (count() !== null) {
        <span class="panel-count">{{ count() }}</span>
      }
      <div class="panel-actions"><ng-content select="[panel-actions]" /></div>
    </div>
    <ng-content />`,
})
export class Panel {
  readonly heading = input.required<string>();
  readonly icon = input('');
  readonly count = input<number | null>(null);
  readonly id = `nx-panel-${++sequence}`;
}
