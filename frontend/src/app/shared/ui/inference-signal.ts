import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'nx-inference-signal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.is-subtle]': 'subtle()' },
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      width: 2.5rem;
      height: 1.25rem;
    }
    :host(.is-subtle) {
      width: 2rem;
      height: 1rem;
    }
  `,
  template: `<svg
    viewBox="0 0 48 24"
    class="inference-signal"
    [class.active]="active()"
    [class.is-subtle]="subtle()"
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
  readonly subtle = input(false);
}
