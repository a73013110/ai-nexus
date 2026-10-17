import { NgComponentOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from '@shared/ui/icon';
import type { PlacedTile } from './dashboard-layout';
import { TILE_SIZES, type TileSize } from '../widgets/dashboard-widgets';

/**
 * 看板的一塊磚塊：環繞其小工具的有標題表面。編輯模式下會顯示移動把手（拖曳或方向鍵）、
 * 所有大小、高度與隱藏的控制；順序由看板決定。窄磚塊上，工具會換行到標題下方而非隱藏，
 * 讓小磚塊隨時都能再放大。
 */
@Component({
  selector: 'nx-dashboard-tile',
  imports: [Icon, NgComponentOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './dashboard-tile.scss',
  host: {
    role: 'region',
    '[attr.aria-labelledby]': "'tile-' + tile().id + '-title'",
    '[attr.data-id]': 'tile().id',
    '[attr.data-size]': 'tile().size',
    '[class.is-tall]': 'tile().tall',
    '[class.is-editing]': 'editing()',
  },
  template: `<header class="tile-head">
      <span class="tile-icon"><nx-icon [name]="tile().widget.icon" /></span>
      <h2 [id]="'tile-' + tile().id + '-title'">{{ tile().widget.title }}</h2>
      @if (editing()) {
        <div class="tile-tools">
          <div class="tile-sizes" role="group" [attr.aria-label]="tile().widget.title + ' 大小'">
            @for (size of sizes; track size.value) {
              <button
                type="button"
                [attr.aria-pressed]="tile().size === size.value"
                (click)="sizeChange.emit(size.value)"
              >
                {{ size.label }}
              </button>
            }
          </div>
          <button
            type="button"
            class="tile-tool"
            [attr.aria-pressed]="tile().tall"
            [attr.aria-label]="(tile().tall ? '恢復高度：' : '加高：') + tile().widget.title"
            (click)="tall.emit(!tile().tall)"
          >
            <nx-icon [name]="tile().tall ? 'minimize' : 'maximize'" />
          </button>
          <button
            type="button"
            class="tile-tool"
            [attr.aria-label]="'隱藏 ' + tile().widget.title"
            (click)="hide.emit()"
          >
            <nx-icon name="eye-off" />
          </button>
          <button
            type="button"
            class="tile-tool tile-handle"
            [attr.data-handle]="tile().id"
            [attr.aria-label]="'移動 ' + tile().widget.title + '，用方向鍵調整位置'"
            (pointerdown)="grab.emit($event)"
            (keydown)="key($event)"
          >
            <nx-icon name="grip" />
          </button>
        </div>
      }
    </header>
    <div class="tile-body" [attr.inert]="editing() ? '' : null">
      <ng-container
        [ngComponentOutlet]="tile().widget.component"
        [ngComponentOutletInputs]="tile().widget.inputs ?? {}"
      />
    </div>`,
})
export class DashboardTile {
  readonly tile = input.required<PlacedTile>();
  readonly editing = input(false);
  readonly sizes = TILE_SIZES;
  readonly grab = output<PointerEvent>();
  readonly nudge = output<number | 'first' | 'last'>();
  readonly sizeChange = output<TileSize>();
  readonly tall = output<boolean>();
  readonly hide = output<void>();
  key(event: KeyboardEvent) {
    const offset = { ArrowLeft: -1, ArrowUp: -1, ArrowRight: 1, ArrowDown: 1 }[event.key];
    const edge = { Home: 'first', End: 'last' }[event.key] as 'first' | 'last' | undefined;
    if (!offset && !edge) return;
    event.preventDefault();
    this.nudge.emit(edge ?? offset!);
  }
}
