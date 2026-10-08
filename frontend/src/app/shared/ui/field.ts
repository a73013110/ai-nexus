import { Directive } from '@angular/core';

/** Native form semantics and Angular forms, with the shared control tokens. */
@Directive({
  selector: 'input[nxField], textarea[nxField], select[nxField]',
  host: { class: 'ui-input' },
})
export class Field {}
