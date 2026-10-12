import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';

/**
 * One resource in a gallery, on the link that opens it: an icon, the name, an optional two-line
 * description and the feature's meta line (projected). The whole card is the link's target.
 */
@Component({
  selector: 'a[nxResourceCard]',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './resource-card.scss',
  host: { class: 'resource-card' },
  template: `<span class="card-top"
      ><span class="card-icon"><nx-icon [name]="icon()" /></span><ng-content select="[card-badge]"
    /></span>
    <strong class="card-name">{{ heading() }}</strong>
    @if (description()) {
      <span class="card-description">{{ description() }}</span>
    }
    <span class="card-meta"><ng-content /></span>`,
})
export class ResourceCard {
  readonly heading = input.required<string>();
  readonly icon = input('document');
  readonly description = input('');
}
