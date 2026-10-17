import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** 3×3 矩陣中每個點的對角索引（列 + 欄）；它讓波浪錯開。 */
const DOTS = [0, 1, 2, 1, 2, 3, 2, 3, 4];

/** 外層的工作流程負責即時播報；這個元件只呈現其狀態。 */
@Component({
  selector: 'nx-generation-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './generation-indicator.scss',
  template: `<div class="generation-indicator" [class.is-waiting]="waiting()">
    <span class="generation-matrix" aria-hidden="true">
      @for (step of dots; track $index) {
        <i [style.--step]="step"></i>
      }
    </span>
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
  protected readonly dots = DOTS;
}
