import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Icon } from '@shared/ui/icon';
import { CountBadge } from '@shared/ui/count-badge';
import { uniqueId } from '@shared/browser/unique-id';
import { badgeDescription, type NavigationBadge } from './navigation-badges';

/** row：一行（圖示在左）；tile：圖示格（名稱在下）；icon：只有圖示，名稱只在提示與無障礙名稱。 */
export type NavigationEntryLayout = 'row' | 'tile' | 'icon';

/**
 * 側欄的一個入口，釘選列、全部功能與圖示欄共用：圖示與右上角的徽章、已釘選的小圖釘、名稱、外部連結標記。
 * 掛在 `<a>` 或 `<button>` 上；外框的尺寸、hover 與選取底色由所在的列表決定，這裡只排內容。
 * 無障礙名稱固定是名稱（加上 `note`），徽章數量放在描述，名稱不會隨數量變動。
 */
@Component({
  selector: 'a[nxNavigationEntry], button[nxNavigationEntry]',
  imports: [Icon, CountBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './navigation-entry.scss',
  host: {
    class: 'navigation-entry',
    '[class.is-row]': "layout() === 'row'",
    '[class.is-tile]': "layout() === 'tile'",
    '[class.is-icon]': "layout() === 'icon'",
    '[attr.title]': 'title()',
    '[attr.aria-label]': 'label() || name() + note()',
    '[attr.aria-describedby]': 'badge() === null ? null : descriptionId',
  },
  template: `<span class="entry-icon">
      <nx-icon [name]="icon()" />
      @if (badge(); as badge) {
        <nx-count-badge
          tone="danger"
          [overlay]="true"
          [dot]="badge === 'dot'"
          [count]="badge === 'dot' ? 1 : badge"
        />
      }
    </span>
    <span class="entry-label">
      @if (pinned()) {
        <nx-icon class="entry-pin" name="pin" aria-hidden="true" />
      }
      <span class="entry-name">{{ name() }}</span>
    </span>
    @if (external()) {
      <nx-icon class="entry-external" name="external" aria-hidden="true" />
    }
    @if (badge() !== null) {
      <span class="sr-only" [id]="descriptionId">{{ description() }}</span>
    }`,
})
export class NavigationEntry {
  readonly name = input.required<string>();
  readonly icon = input.required<string>();
  readonly layout = input<NavigationEntryLayout>('row');
  readonly badge = input<NavigationBadge | null>(null);
  readonly pinned = input(false);
  readonly external = input(false);
  /** 接在名稱後的補充（例：「（外部連結，開新分頁）」），同時進入提示與無障礙名稱。 */
  readonly note = input('');
  /** 無障礙名稱與名稱不同時（例：編輯釘選的「釘選對話」）。 */
  readonly label = input('');
  readonly descriptionId = uniqueId('navigation-entry-badge');
  readonly description = computed(() => badgeDescription(this.badge()));
  /** 名稱可能被截斷或只顯示圖示，提示一律給完整名稱。 */
  readonly title = computed(() => this.name() + this.note() + (this.pinned() ? '（已釘選）' : ''));
}
