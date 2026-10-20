import {
  Injectable,
  InjectionToken,
  computed,
  inject,
  type Provider,
  type Signal,
} from '@angular/core';

/**
 * 側欄入口上的提醒：數字顯示數量（超過 99 顯示 99+），`'dot'` 只表示有新動態（數量不重要或算不準時用）。
 * 0 與沒有項目一樣不顯示。
 */
export type NavigationBadge = number | 'dot';
/** 一個來源回報的徽章，鍵是功能 ID（與釘選、`aria-current` 同一個單位）。 */
export type NavigationBadgeMap = ReadonlyMap<string, NavigationBadge>;

export const NAVIGATION_BADGE_SOURCES = new InjectionToken<readonly Signal<NavigationBadgeMap>[]>(
  'NAVIGATION_BADGE_SOURCES',
);

/**
 * 註冊一個徽章來源；在 `app.config.ts` 呼叫，`source` 在注入脈絡中執行。
 * 來源必須是全站單例的 signal：選單在功能頁面延遲載入前就要顯示徽章，不能依賴頁面的 store。
 */
export function provideNavigationBadges(source: () => Signal<NavigationBadgeMap>): Provider {
  return { provide: NAVIGATION_BADGE_SOURCES, multi: true, useFactory: source };
}

/** 合併所有來源的徽章：數字相加；只有圓點時顯示圓點，有數字時數字優先。 */
@Injectable({ providedIn: 'root' })
export class NavigationBadges {
  private readonly sources = inject(NAVIGATION_BADGE_SOURCES, { optional: true }) ?? [];
  readonly all = computed(() => {
    const merged = new Map<string, NavigationBadge>();
    for (const source of this.sources)
      for (const [feature, badge] of source()) {
        const count = typeof badge === 'number' && Number.isFinite(badge) ? Math.trunc(badge) : 0;
        if (badge !== 'dot' && count <= 0) continue;
        const previous = merged.get(feature);
        merged.set(
          feature,
          badge === 'dot'
            ? (previous ?? 'dot')
            : (typeof previous === 'number' ? previous : 0) + count,
        );
      }
    return merged as NavigationBadgeMap;
  });
  of(feature: string | null | undefined): NavigationBadge | null {
    return (feature && this.all().get(feature)) || null;
  }
  /** 任一功能有徽章；收起來的地方（「全部功能」、收合的分群）用圓點提示裡面有東西。 */
  any(features: Iterable<string>) {
    const all = this.all();
    for (const feature of features) if (all.has(feature)) return true;
    return false;
  }
}

/** 給螢幕閱讀器的徽章說明；名稱維持功能名稱，數量放在描述裡。 */
export function badgeDescription(badge: NavigationBadge | null): string {
  if (badge === null) return '';
  if (badge === 'dot') return '有新動態';
  return `${badge > 99 ? '超過 99' : badge} 則新動態`;
}
