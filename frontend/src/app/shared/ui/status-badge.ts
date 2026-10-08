import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { BadgeTone } from './count-badge';

@Component({
  selector: 'nx-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()' },
  styles: `
    :host {
      --badge-color: var(--secondary);
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.125rem 0.5rem;
      border-radius: var(--p-radius-sm);
      color: var(--badge-color);
      background: color-mix(in srgb, var(--badge-color) 9%, var(--surface));
      font-size: var(--p-font-xs);
      font-weight: 600;
      line-height: 1.6;
      white-space: nowrap;
    }
    :host([data-tone='info']) {
      --badge-color: var(--info);
    }
    :host([data-tone='success']) {
      --badge-color: var(--success);
    }
    :host([data-tone='warning']) {
      --badge-color: var(--warning);
    }
    :host([data-tone='danger']) {
      --badge-color: var(--danger);
    }
    .dot {
      width: 0.375rem;
      height: 0.375rem;
      border-radius: 50%;
      background: currentColor;
    }
  `,
  template: `<span class="dot" aria-hidden="true"></span><ng-content />`,
})
export class StatusBadge {
  readonly tone = input<BadgeTone>('neutral');
}
