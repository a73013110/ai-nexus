import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import type { BadgeTone } from './count-badge';

/**
 * 狀態標籤。給了 `state`（狀態識別）時，自動更新造成的狀態變化（例：寄送中→已寄出）播一次彈簧收尾的確認動效；
 * 第一次顯示與減少動態時不播，顏色變化本來就有過渡。
 */
@Component({
  selector: 'nx-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()', '[attr.data-changed]': 'changed() ? "" : null' },
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
      transition:
        color var(--motion-panel) var(--ease),
        background-color var(--motion-panel) var(--ease);
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
    :host([data-changed]) {
      animation: status-settle var(--motion-morph) var(--ease-spring);
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
  /** 狀態識別（例：`sent`）；省略時不播變化動效。 */
  readonly state = input<string>();
  readonly changed = signal(false);

  constructor() {
    let previous: string | undefined;
    let frame = 0;
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(frame));
    effect(() => {
      const state = this.state();
      if (state === undefined) return;
      const before = previous;
      previous = state;
      if (before === undefined || before === state) return;
      // 先移除再於下一幀加回，連續變化時動畫才會重播。
      this.changed.set(false);
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => this.changed.set(true));
    });
  }
}
