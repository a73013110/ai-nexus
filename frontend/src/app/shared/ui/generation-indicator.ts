import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Diagonal index (row + column) of each dot in the 3×3 matrix; it staggers the wave. */
const DOTS = [0, 1, 2, 1, 2, 3, 2, 3, 4];

/** The surrounding workflow owns live announcements; this component only presents its state. */
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
