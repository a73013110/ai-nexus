import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'nx-inference-signal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<svg
    viewBox="0 0 48 24"
    class="inference-signal"
    [class.active]="active()"
    aria-hidden="true"
  >
    <path class="signal-track" d="M2 12h10l6-7h12l6 7h10M12 12l6 7h12l6-7" />
    <path class="signal-flow" d="M2 12h10l6-7h12l6 7h10" />
    <path class="signal-flow secondary" d="M2 12h10l6 7h12l6-7h10" />
    <circle cx="2" cy="12" r="1.7" />
    <circle cx="46" cy="12" r="1.7" />
  </svg>`,
})
export class InferenceSignal {
  readonly active = input(false);
}
