import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'nx-thinking-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="thinking-indicator" role="status">
    <span class="thinking-spectrum" aria-hidden="true">
      <i></i><i></i><i></i><i></i><i></i><i></i><i></i></span
    ><span class="thinking-label"
      >{{ label() }}<small>{{ detail() }}</small></span
    >
  </div>`,
})
export class ThinkingIndicator {
  readonly label = input('正在思考');
  readonly detail = input('正在整理資訊，回答將逐步呈現');
}
