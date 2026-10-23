import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { SearchField } from './search-field';
import { ViewSwitch, type ViewOption } from './view-switch';

/**
 * 集合的索引：一列安靜的工具列（可選的分段篩選、功能自己的工具與搜尋），下方是卡片格狀排列。
 * 首次載入（`loading`）畫格狀骨架；已有卡片時重讀（`busy`）保留舊卡片、淡化並在上緣掃過細線，
 * 搜尋欄同時轉圈。沒有卡片又在讀取時也畫骨架，搜尋還沒回來前不先說「沒有符合」。
 * 確定沒有符合項目時，改顯示功能的 `[gallery-empty]` 內容。
 */
@Component({
  selector: 'nx-resource-gallery',
  imports: [SearchField, ViewSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './resource-gallery.scss',
  template: `<div class="gallery-toolbar" role="group" [attr.aria-label]="label() + '條件'">
      @if (filters().length) {
        <nx-view-switch
          appearance="segment"
          [label]="label() + '範圍'"
          [options]="filters()"
          [value]="filter()"
          (valueChange)="filterChange.emit($event)"
        />
      }
      <ng-content select="[gallery-tools]" />
      <nx-search-field
        class="gallery-search"
        [label]="'搜尋' + label()"
        [placeholder]="placeholder()"
        [value]="search()"
        [busy]="busy()"
        (valueChange)="searchChange.emit($event)"
      />
    </div>
    <span class="refresh-line" [class.is-active]="refreshing()" aria-hidden="true"></span>
    @if (loading() || (busy() && empty())) {
      <div class="gallery-grid" role="status" [attr.aria-label]="'正在載入' + label()">
        @for (slot of slots; track slot) {
          <span class="skeleton gallery-skeleton"></span>
        }
      </div>
    } @else if (empty()) {
      <ng-content select="[gallery-empty]" />
    } @else {
      <div
        class="gallery-grid"
        [class.is-refreshing]="refreshing()"
        [attr.aria-busy]="refreshing() || null"
      >
        <ng-content />
      </div>
    }`,
})
export class ResourceGallery {
  /** What the gallery holds, e.g. 專案; names the toolbar, search and loading status. */
  readonly label = input.required<string>();
  readonly placeholder = input('');
  readonly search = input('');
  readonly filters = input<readonly ViewOption[]>([]);
  readonly filter = input('');
  /** 首次載入：還沒有任何卡片，畫骨架。綁 `apiResource` 的 `loading()` 且沒有資料時。 */
  readonly loading = input(false);
  /** 重新讀取（重讀、搜尋、篩選、翻頁）：綁 `refreshing()`，有防抖的搜尋再加上打字中。 */
  readonly busy = input(false);
  readonly empty = input(false);
  readonly searchChange = output<string>();
  readonly filterChange = output<string>();
  readonly slots = [0, 1, 2, 3, 4, 5];
  /** 淡化只給已有卡片的格線；骨架與空狀態不疊淡化，否則像停用。 */
  protected readonly refreshing = computed(() => this.busy() && !this.loading() && !this.empty());
}
