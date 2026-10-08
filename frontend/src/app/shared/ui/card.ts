import { Directive } from '@angular/core';

/** A neutral surface; its feature owns the content layout and native element semantics. */
@Directive({
  selector: '[nxCard]',
  host: { class: 'ui-card' },
})
export class Card {}
