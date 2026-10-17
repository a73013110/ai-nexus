import { Directive } from '@angular/core';

/** 中性的表面；內容版面與原生元素語意由其功能負責。 */
@Directive({
  selector: '[nxCard]',
  host: { class: 'ui-card' },
})
export class Card {}
