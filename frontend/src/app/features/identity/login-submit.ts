import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from '../../shared/ui/icon';

export type SubmitPhase = 'idle' | 'loading' | 'success';

/** A sign-in button whose content and shape morph from its label to loading to a docked check. */
@Component({
  selector: 'button[nxLoginSubmit]',
  styleUrl: './login-submit.scss',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'primary-button login-submit', '[attr.data-phase]': 'phase()' },
  template: `@switch (phase()) {
    @case ('loading') {
      <span class="submit-state"
        ><nx-icon name="loading" class="submit-spinner" />{{ busy() }}</span
      >
    }
    @case ('success') {
      <span class="submit-state"
        ><nx-icon name="check" /><span class="sr-only">登入成功</span></span
      >
    }
    @default {
      <span class="submit-state"><ng-content /></span>
    }
  }`,
})
export class LoginSubmit {
  readonly phase = input<SubmitPhase>('idle');
  readonly busy = input.required<string>();
}
