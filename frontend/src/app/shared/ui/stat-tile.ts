import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { BadgeTone } from './count-badge';

/**
 * 一列標題數字：能放下幾欄就放幾欄（`--stat-tile-min`，預設 10rem），手機上為兩欄。
 * 格線用列表實作，讓輔助科技能宣告後面還有幾個數字。
 */
@Component({
  selector: 'nx-stat-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'list', '[attr.aria-label]': 'label()' },
  styles: `
    :host {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, var(--stat-tile-min, 10rem)), 1fr));
      gap: var(--p-space-3);
      min-width: 0;
    }
  `,
  template: `<ng-content />`,
})
export class StatGrid {
  readonly label = input.required<string>();
}

/**
 * `StatGrid` 中的一個標題數字：安靜的標籤、依磚塊縮放的等寬數字（不會溢出磚塊），
 * 可選的單位與一行脈絡。功能傳入已格式化的值；
 * `null` 顯示同高度的載入占位；縮短的數字會把完整值放在 `exact`。只有真正的警告或危險才會為磚塊上色。
 */
@Component({
  selector: 'nx-stat-tile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './stat-tile.scss',
  host: { role: 'listitem', '[attr.data-tone]': 'tone()', '[attr.aria-busy]': 'value() === null' },
  template: `<span class="stat-label">{{ label() }}</span>
    @if (value() === null) {
      <span class="skeleton stat-skeleton"></span><span class="sr-only">正在載入</span>
    } @else {
      <strong class="stat-value" [attr.title]="exact() || null"
        ><span class="stat-number">{{ value() }}</span>
        @if (unit()) {
          <em>{{ unit() }}</em>
        }
      </strong>
    }
    @if (hint()) {
      <small class="stat-hint">{{ hint() }}</small>
    }
    <ng-content />`,
})
export class StatTile {
  readonly label = input.required<string>();
  readonly value = input.required<string | null>();
  readonly unit = input('');
  readonly hint = input('');
  readonly exact = input('');
  readonly tone = input<BadgeTone>('neutral');
}
