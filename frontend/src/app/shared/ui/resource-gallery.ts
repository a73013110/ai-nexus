import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { SearchField } from './search-field';
import { ViewSwitch, type ViewOption } from './view-switch';

/**
 * A collection's index: one quiet toolbar (an optional segment filter, the feature's own tools and
 * a search) over a grid of cards. While loading it holds the grid's shape; when nothing matches it
 * shows the feature's `[gallery-empty]` content instead.
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
