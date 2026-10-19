import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlidingIndicator } from './sliding-indicator';

export interface ViewLink {
  value: string;
  label: string;
  link: string;
}

/**
 * 外觀與 `ViewSwitch`（底線）相同的一列連結，給同一模組的幾個頁面互相切換：每個選項是自己的網址，
 * 可以開新分頁、重新整理後停在同一頁。選取的連結標 `aria-current="page"`，指示器跟著它滑動。
 */
@Component({
  selector: 'nx-view-links',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './view-switch.scss',
  hostDirectives: [SlidingIndicator],
  host: { class: 'ui-view-switch', role: 'navigation', '[attr.aria-label]': 'label()' },
  template: `@for (option of options(); track option.value) {
    <a [routerLink]="option.link" [attr.aria-current]="value() === option.value ? 'page' : null">{{
      option.label
    }}</a>
  }`,
})
export class ViewLinks {
  readonly label = input.required<string>();
  readonly options = input.required<readonly ViewLink[]>();
  readonly value = input.required<string>();
}
