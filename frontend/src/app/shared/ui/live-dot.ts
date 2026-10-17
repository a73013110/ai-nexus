import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { BadgeTone } from './count-badge';

/**
 * 小型狀態點。`live` 時，同色的光暈會從它向外擴散並淡出，速度緩慢，且只變化透明度與縮放；
 * 暫停或警告狀態則維持為普通的點。這是裝飾：旁邊的文字才承載狀態。
 */
@Component({
  selector: 'nx-live-dot',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'live-dot',
    'aria-hidden': 'true',
    '[attr.data-tone]': 'tone()',
    '[class.is-live]': 'live()',
  },
  styles: `
    :host {
      --live-dot-color: var(--muted);
      position: relative;
      display: inline-block;
      flex: none;
      width: var(--live-dot-size, 0.4375rem);
      height: var(--live-dot-size, 0.4375rem);
      border-radius: 50%;
      background: var(--live-dot-color);
      transition: background-color var(--motion-panel) var(--ease);
    }
    :host([data-tone='info']) {
      --live-dot-color: var(--accent);
    }
    :host([data-tone='success']) {
      --live-dot-color: var(--success);
    }
    :host([data-tone='warning']) {
      --live-dot-color: var(--warning);
    }
    :host([data-tone='danger']) {
      --live-dot-color: var(--danger);
    }
    :host::after {
      content: '';
      position: absolute;
      inset: 0;
      border-radius: inherit;
      background: inherit;
      opacity: 0;
    }
    :host(.is-live)::after {
      animation: halo-breathe var(--motion-signal) var(--ease) infinite;
    }
    @media (prefers-reduced-motion: reduce) {
      :host(.is-live)::after {
        animation: none;
      }
    }
    :host-context([data-reduced-motion='true'])::after {
      animation: none;
    }
    @media (forced-colors: active) {
      :host {
        background: CanvasText;
      }
    }
  `,
  template: '',
})
export class LiveDot {
  readonly tone = input<BadgeTone>('neutral');
  readonly live = input(false);
}
