import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  viewChild,
} from '@angular/core';
import { NavigationEnd, PRIMARY_OUTLET, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { NgTemplateOutlet } from '@angular/common';
import { Icon } from '@shared/ui/icon';
import { CountBadge } from '@shared/ui/count-badge';
import { ScrollArea } from '@shared/ui/scroll-area';
import { anchoredPopover } from '@shared/browser/anchored-popover';
import { setPopoverOrigin } from '@shared/browser/popover-position';
import { uniqueId } from '@shared/browser/unique-id';
import { FeatureLauncher } from './feature-launcher';
import { NavigationPreferences } from './navigation-preferences';
import { NavigationBadges } from './navigation-badges';
import { NavigationEntry } from './navigation-entry';
import { featureIcon } from './navigation-tree';

/**
 * Pinned features on top, then “全部功能”: inline when the page has no sidebar panel, a disclosure
 * 會取代面板（若有）的浮出層，以及圖示欄旁的飛出層。圖示顯示時會把釘選排成一列，讓對話歷史保持高度；
 * 列表顯示時，每個功能各佔一行。
 */
@Component({
  selector: 'nx-workspace-navigation',
  host: { '[class.is-compact]': 'compact()' },
  imports: [
    RouterLink,
    NgTemplateOutlet,
    Icon,
    CountBadge,
    ScrollArea,
    FeatureLauncher,
    NavigationEntry,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './workspace-navigation.scss',
  template: `<nav
    class="workspace-navigation ui-density-compact"
    aria-label="工作區功能"
    [class.collapsible]="collapsible()"
  >
    <!-- 「全部功能」收起來時，沒釘選的功能有徽章就在它的圖示上提示一個圓點。 -->
    <ng-template #launcherIcon let-closed>
      <span class="icon-badge-anchor">
        <nx-icon name="apps" />
        @if (closed && hiddenBadge()) {
          <nx-count-badge tone="danger" [dot]="true" [count]="1" [overlay]="true" />
        }
      </span>
    </ng-template>
    @if (hiddenBadge()) {
      <span class="sr-only" [id]="hiddenBadgeId">其他功能有新動態</span>
    }
    <nx-scroll-area label="工作區功能">
      <ul
        class="pinned-features"
        aria-label="釘選功能"
        [class.is-strip]="strip()"
        [class.has-active]="activeIndex() >= 0"
        [style.--active-index]="activeIndex()"
        [style.--slots]="pins.pinned().length + (strip() && collapsible() ? 1 : 0)"
      >
        @for (feature of pins.pinned(); track feature.id) {
          <li>
            <!-- 釘選列裡都是已釘選的功能，不再顯示圖釘。 -->
            <a
              nxNavigationEntry
              [routerLink]="feature.route"
              [class.current]="activeFeature() === feature.id"
              [attr.aria-current]="activeFeature() === feature.id ? 'page' : null"
              [layout]="strip() || compact() ? 'icon' : 'row'"
              [name]="feature.name"
              [icon]="icon(feature.icon)"
              [badge]="badges.of(feature.id)"
              (click)="activated.emit()"
            ></a>
          </li>
        }
        @if (strip() && collapsible()) {
          <li>
            <button
              type="button"
              class="launcher-toggle"
              aria-label="全部功能"
              title="全部功能"
              [class.is-open]="expanded()"
              [attr.aria-expanded]="expanded()"
              [attr.aria-describedby]="hiddenBadge() && !expanded() ? hiddenBadgeId : null"
              (click)="expandedChange.emit(!expanded())"
            >
              <ng-container
                [ngTemplateOutlet]="launcherIcon"
                [ngTemplateOutletContext]="{ $implicit: !expanded() }"
              />
            </button>
          </li>
        }
      </ul>
      @if (compact()) {
        <button
          #flyoutTrigger
          type="button"
          class="launcher-trigger"
          aria-label="全部功能"
          title="全部功能"
          [class.current]="activeIndex() < 0 && !!activeFeature()"
          [attr.popovertarget]="flyoutId"
          [attr.aria-describedby]="hiddenBadge() && !flyoutOpen() ? hiddenBadgeId : null"
        >
          <ng-container
            [ngTemplateOutlet]="launcherIcon"
            [ngTemplateOutletContext]="{ $implicit: !flyoutOpen() }"
          />
        </button>
      } @else if (collapsible() && !strip()) {
        <button
          type="button"
          class="workspace-disclosure"
          [attr.aria-expanded]="expanded()"
          [attr.aria-describedby]="hiddenBadge() && !expanded() ? hiddenBadgeId : null"
          (click)="expandedChange.emit(!expanded())"
        >
          <ng-container
            [ngTemplateOutlet]="launcherIcon"
            [ngTemplateOutletContext]="{ $implicit: !expanded() }"
          /><strong>全部功能</strong>
          <nx-icon name="chevron" />
        </button>
      } @else if (!collapsible() || expanded()) {
        <h2 class="launcher-title">全部功能</h2>
      }
      @if (!compact() && (!collapsible() || expanded())) {
        <nx-feature-launcher [active]="activeFeature()" (activated)="activated.emit()" />
      }
    </nx-scroll-area>
    @if (compact()) {
      <div
        #flyout
        popover="auto"
        class="popover-panel launcher-flyout"
        role="dialog"
        aria-label="全部功能"
        [id]="flyoutId"
        (beforetoggle)="$any($event).newState === 'open' && placeFlyout(flyout)"
        (toggle)="flyoutToggled($event)"
      >
        <h2>全部功能</h2>
        @if (flyoutOpen()) {
          <nx-feature-launcher [active]="activeFeature()" (activated)="closeFlyout()" />
        }
      </div>
    }
  </nav>`,
})
export class WorkspaceNavigation {
  readonly pins = inject(NavigationPreferences);
  readonly badges = inject(NavigationBadges);
  private readonly router = inject(Router);
  private readonly navigation = toSignal(
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)),
  );
  readonly collapsible = input(false);
  readonly compact = input(false);
  readonly activated = output<void>();
  readonly expanded = input(false);
  readonly expandedChange = output<boolean>();
  readonly flyoutId = uniqueId('launcher-flyout');
  readonly hiddenBadgeId = uniqueId('launcher-badge');
  /** 沒釘選的功能有徽章：「全部功能」收起來時看不到它們，由觸發鈕代為提示。 */
  readonly hiddenBadge = computed(() =>
    this.badges.any(
      this.pins
        .pages()
        .map((page) => page.id)
        .filter((id) => !this.pins.has(id)),
    ),
  );
  /** 圖示顯示時，把釘選（與啟動器切換）並排放在同一列。 */
  readonly strip = computed(() => !this.compact() && this.pins.display() === 'icons');
  private readonly flyout = viewChild<ElementRef<HTMLElement>>('flyout');
  private readonly flyoutTrigger = viewChild<ElementRef<HTMLElement>>('flyoutTrigger');
  private readonly flyoutPopover = anchoredPopover({
    panel: () => this.flyout()?.nativeElement,
    place: (flyout) => this.placeFlyout(flyout),
  });
  readonly flyoutOpen = this.flyoutPopover.opened;
  private readonly injector = inject(Injector);
  /**
   * 選取最深的符合頁面；它自己的細節路由仍屬於它。只看選單中的頁面：外部連結沒有本站路由，
   * 不參與判斷。
   */
  readonly activeFeature = computed(() => {
    this.navigation();
    let active: string | null = null;
    let depth = -1;
    for (const feature of this.pins.pages()) {
      const tree = this.router.parseUrl(feature.route);
      if (
        !this.router.isActive(tree, {
          paths: 'subset',
          queryParams: 'ignored',
          matrixParams: 'ignored',
          fragment: 'ignored',
        })
      )
        continue;
      const candidateDepth = tree.root.children[PRIMARY_OUTLET]?.segments.length ?? 0;
      if (candidateDepth > depth) {
        active = feature.id;
        depth = candidateDepth;
      }
    }
    return active;
  });
  /** 滑動膠囊的那一列；目前的功能沒有釘選時為 -1，隱藏它。 */
  readonly activeIndex = computed(() => {
    const active = this.activeFeature();
    return active ? this.pins.ids().indexOf(active) : -1;
  });
  readonly icon = featureIcon;
  /** 在圖示欄旁開啟，與觸發器對齊，並保持在視口內。 */
  placeFlyout(flyout: HTMLElement) {
    const trigger = this.flyoutTrigger()?.nativeElement;
    if (!trigger) return;
    const anchor = trigger.getBoundingClientRect();
    const rail = trigger.closest('.workspace-sidebar')?.getBoundingClientRect() ?? anchor;
    flyout.style.left = `${rail.right + 8}px`;
    flyout.style.maxHeight = `${innerHeight - 24}px`;
    const top = Math.max(12, Math.min(anchor.top - 8, innerHeight - flyout.offsetHeight - 12));
    flyout.style.top = `${top}px`;
    setPopoverOrigin(flyout, 'right', 0, anchor.top + anchor.height / 2 - top);
  }
  flyoutToggled(event: Event) {
    this.flyoutPopover.sync(event);
    if (this.flyoutOpen())
      afterNextRender(
        () => {
          this.flyoutPopover.reposition();
          this.flyout()?.nativeElement.querySelector<HTMLElement>('a[href]')?.focus();
        },
        { injector: this.injector },
      );
  }
  closeFlyout() {
    this.flyoutPopover.hide();
    this.activated.emit();
  }
}
