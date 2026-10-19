import { uniqueId } from '../browser/unique-id';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';

/**
 * 總覽磚塊外觀中的有標題資源表面：安靜的標題（可選圖示與數量），右側是動作，接著是功能的內容。
 * 其區域由標題命名。
 * 數量、右側的動作，接著是功能的內容。其區域由標題命名。
 */
@Component({
  selector: 'nx-panel',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './panel.scss',
  host: { role: 'region', '[attr.aria-labelledby]': 'id' },
  template: `<div class="panel-head">
      @if (icon()) {
        <nx-icon class="panel-icon" [name]="icon()" />
      }
      <h2 [id]="id">{{ heading() }}</h2>
      @if (count() !== null) {
        <span class="panel-count">{{ count() }}</span>
      }
      <div class="panel-actions"><ng-content select="[panel-actions]" /></div>
    </div>
    <ng-content />`,
})
export class Panel {
  readonly heading = input.required<string>();
  readonly icon = input('');
  readonly count = input<number | null>(null);
  readonly id = uniqueId('nx-panel');
}
