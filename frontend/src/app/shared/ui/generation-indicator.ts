import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** The surrounding workflow owns live announcements; this component only presents its state. */
@Component({
  selector: 'nx-generation-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './generation-indicator.scss',
  template: `<div class="generation-indicator" [class.is-waiting]="waiting()">
    <span class="generation-orbit" aria-hidden="true"><i></i><i></i><i></i><b></b></span>
    <span class="generation-label"
      >{{ label() }}<small>{{ detail() }}</small></span
    >
  </div>`,
})
export class GenerationIndicator {
  readonly waiting = input(false);
  readonly label = input('正在準備回答');
  readonly detail = input('尚未收到回答內容，可隨時停止');
}
