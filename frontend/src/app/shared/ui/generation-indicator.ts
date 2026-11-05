import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AiMark } from './ai-mark';

/** 外層的工作流程負責即時播報；這個元件只呈現其狀態。 */
@Component({
  selector: 'nx-generation-indicator',
  imports: [AiMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './generation-indicator.scss',
  template: `<div class="generation-indicator" [class.is-waiting]="waiting()">
    <nx-ai-mark class="generation-mark" [state]="waiting() ? 'waiting' : 'working'" />
    <span class="generation-label"
      ><span class="generation-title">{{ label() }}</span
      ><small>{{ detail() }}</small></span
    >
  </div>`,
})
export class GenerationIndicator {
  readonly waiting = input(false);
  readonly label = input('正在準備回答');
  readonly detail = input('尚未收到回答內容，可隨時停止');
}
