import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';

/**
 * 圖庫中的一個資源，其連結即為開啟它的入口：圖示、名稱、可選的兩行說明，
 * 以及功能的中繼資料列（投影）。整張卡片都是連結的目標。
 */
@Component({
  selector: 'a[nxResourceCard]',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './resource-card.scss',
  host: { class: 'resource-card' },
  template: `<span class="card-top"
      ><span class="card-icon"><nx-icon [name]="icon()" /></span><ng-content select="[card-badge]"
    /></span>
    <strong class="card-name">{{ heading() }}</strong>
    @if (description()) {
      <span class="card-description">{{ description() }}</span>
    }
    <span class="card-meta"><ng-content /></span>`,
})
export class ResourceCard {
  readonly heading = input.required<string>();
  readonly icon = input('document');
  readonly description = input('');
}
