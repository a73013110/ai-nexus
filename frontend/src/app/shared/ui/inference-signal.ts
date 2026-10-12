import { ChangeDetectionStrategy, Component, input } from '@angular/core';

let sequence = 0;

/** Model activity: a pulse runs along two paths and shifts hue across the model-activity sweep. */
@Component({
  selector: 'nx-inference-signal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './inference-signal.scss',
  host: { '[class.is-subtle]': 'subtle()' },
  template: `<svg
    viewBox="0 0 48 24"
    class="inference-signal"
    [class.active]="active()"
    [class.is-subtle]="subtle()"
    aria-hidden="true"
  >
    <defs>
      <linearGradient [id]="id" gradientUnits="userSpaceOnUse" x1="2" y1="0" x2="46" y2="0">
        <stop offset="0" />
        <stop offset="0.55" />
        <stop offset="1" />
      </linearGradient>
    </defs>
    <path class="signal-track" d="M2 12h10l6-7h12l6 7h10M12 12l6 7h12l6-7" />
    <path class="signal-flow" [attr.stroke]="'url(#' + id + ')'" d="M2 12h10l6-7h12l6 7h10" />
    <path
      class="signal-flow secondary"
      [attr.stroke]="'url(#' + id + ')'"
      d="M2 12h10l6 7h12l6-7h10"
    />
    <circle class="signal-start" cx="2" cy="12" r="1.7" />
    <circle class="signal-end" cx="46" cy="12" r="1.7" />
  </svg>`,
})
export class InferenceSignal {
  readonly active = input(false);
  readonly subtle = input(false);
  readonly id = 'nx-inference-' + ++sequence;
}
