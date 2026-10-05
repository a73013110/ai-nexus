import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'nx-thinking-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
    class="thinking-indicator"
    role="status"
    [class.is-waiting]="mode() === 'waiting'"
  >
    <span class="thinking-spectrum" aria-hidden="true">
      <svg class="thinking-orbit" viewBox="0 0 80 80">
        <circle cx="40" cy="40" r="34" />
        <path d="M40 6a34 34 0 0 1 34 34" />
        <circle class="thinking-orbit-node" cx="40" cy="6" r="3" />
      </svg>
      <i></i><i></i><i></i><i></i><i></i><i></i><i></i></span
    ><span class="thinking-label"
      >{{ label() }}<small>{{ detail() }}</small></span
    >
  </div>`,
})
export class ThinkingIndicator {
  readonly mode = input<'waiting' | 'thinking'>('thinking');
  readonly label = input('正在思考');
  readonly detail = input('正在整理資訊，回答將逐步呈現');
}
