import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';
import type { BadgeTone } from './count-badge';

/** A reusable interactive graph node; the graph owns placement and connections. */
@Component({
  selector: 'button[nxGraphNode]',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './graph-node.scss',
  host: {
    class: 'graph-node',
    '[attr.data-tone]': 'tone()',
    '[attr.title]': 'label() + " · " + detail() + " · " + statusLabel()',
  },
  template: `<span class="graph-node-avatar">
      @if (icon()) {
        <nx-icon [name]="icon()" />
      } @else {
        {{ avatar() }}
      }
    </span>
    <span class="graph-node-copy"
      ><strong>{{ label() }}</strong
      ><small>{{ detail() }}</small></span
    >
    <span class="graph-node-light" aria-hidden="true"></span>`,
})
export class GraphNode {
  readonly label = input.required<string>();
  readonly detail = input.required<string>();
  readonly avatar = input('');
  readonly icon = input('');
  readonly tone = input<BadgeTone>('neutral');
  readonly statusLabel = input('');
}
