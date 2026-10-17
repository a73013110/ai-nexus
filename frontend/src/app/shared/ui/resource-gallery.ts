import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { SearchField } from './search-field';
import { ViewSwitch, type ViewOption } from './view-switch';

/**
 * 集合的索引：一列安靜的工具列（可選的分段篩選、功能自己的工具與搜尋），下方是卡片格狀排列。
 * 載入中時保持格狀的形狀；沒有符合項目時，改顯示功能的 `[gallery-empty]` 內容。
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
        (valueChange)="searchChange.emit($event)"
      />
    </div>
    @if (loading()) {
      <div class="gallery-grid" role="status" [attr.aria-label]="'正在載入' + label()">
        @for (slot of slots; track slot) {
          <span class="skeleton gallery-skeleton"></span>
        }
      </div>
    } @else if (empty()) {
      <ng-content select="[gallery-empty]" />
    } @else {
      <div class="gallery-grid"><ng-content /></div>
    }`,
})
export class ResourceGallery {
  /** What the gallery holds, e.g. 專案; names the toolbar, search and loading status. */
  readonly label = input.required<string>();
  readonly placeholder = input('');
  readonly search = input('');
  readonly filters = input<readonly ViewOption[]>([]);
  readonly filter = input('');
  readonly loading = input(false);
  readonly empty = input(false);
  readonly searchChange = output<string>();
  readonly filterChange = output<string>();
  readonly slots = [0, 1, 2, 3, 4, 5];
}
