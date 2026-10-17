import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Icon } from '@shared/ui/icon';
import { DashboardLayout } from './dashboard-layout';
import { DashboardStore } from '../dashboard-store';

/** 自訂時顯示：如何排列磚塊、要復原的隱藏磚塊、重設與完成。 */
@Component({
  selector: 'nx-dashboard-edit-bar',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './dashboard-edit-bar.scss',
  host: { role: 'region', 'aria-label': '自訂總覽版面' },
  template: `<p>
      拖曳 <nx-icon name="grip" /> 或在其上按方向鍵調整順序；選擇大小、加高或隱藏卡片。
      版面只存在這個瀏覽器，「{{
        store.scope() === 'platform' ? '整個平台' : '我的工作'
      }}」各自保存。
    </p>
    @if (layout.hidden().length) {
      <div class="edit-hidden">
        <span>已隱藏</span>
        @for (tile of layout.hidden(); track tile.id) {
          <button type="button" (click)="layout.setHidden(tile.id, false)">
            <nx-icon name="plus" />{{ tile.widget.title }}
          </button>
        }
      </div>
    }
    <div class="edit-actions">
      @if (layout.customized()) {
        <button type="button" class="quiet-button" (click)="layout.reset()">
          <nx-icon name="reset" />恢復預設
        </button>
      }
      <button type="button" class="primary-button" (click)="layout.editing.set(false)">完成</button>
    </div>`,
})
export class DashboardEditBar {
  readonly layout = inject(DashboardLayout);
  readonly store = inject(DashboardStore);
}
