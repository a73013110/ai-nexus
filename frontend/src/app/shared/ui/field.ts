import { Directive } from '@angular/core';

/** 原生表單語意與 Angular 表單，搭配共用的控制項 token。 */
@Directive({
  selector: 'input[nxField], textarea[nxField], select[nxField]',
  host: { class: 'ui-input' },
})
export class Field {}
