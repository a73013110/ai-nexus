import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { BadgeTone } from './count-badge';

/**
 * A row of headline figures: as many columns as fit (`--stat-tile-min`, 10rem by default), two on
 * a phone. The grid is a list so assistive technology announces how many figures follow.
 */
@Component({
  selector: 'nx-stat-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'list', '[attr.aria-label]': 'label()' },
  styles: `
    :host {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, var(--stat-tile-min, 10rem)), 1fr));
      gap: var(--p-space-3);
      min-width: 0;
    }
  `,
  template: `<ng-content />`,
})
export class StatGrid {
  readonly label = input.required<string>();
}

/**
 * One headline figure in a `StatGrid`: a quiet label, the value in tabular numerals scaled to the
 * tile (never spilling out of it), an optional unit and one line of context. The feature passes the
 * formatted value; `null` shows a loading placeholder of the same height, and a shortened figure
 * puts its full value in `exact`. Only a real warning or danger tints the tile.
 */
@Component({
  selector: 'nx-stat-tile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './stat-tile.scss',
  host: { role: 'listitem', '[attr.data-tone]': 'tone()', '[attr.aria-busy]': 'value() === null' },
  template: `<span class="stat-label">{{ label() }}</span>
    @if (value() === null) {
      <span class="skeleton stat-skeleton"></span><span class="sr-only">正在載入</span>
    } @else {
      <strong class="stat-value" [attr.title]="exact() || null"
        ><span class="stat-number">{{ value() }}</span>
        @if (unit()) {
          <em>{{ unit() }}</em>
        }
      </strong>
    }
    @if (hint()) {
      <small class="stat-hint">{{ hint() }}</small>
    }
    <ng-content />`,
})
export class StatTile {
  readonly label = input.required<string>();
  readonly value = input.required<string | null>();
  readonly unit = input('');
  readonly hint = input('');
  readonly exact = input('');
  readonly tone = input<BadgeTone>('neutral');
}
