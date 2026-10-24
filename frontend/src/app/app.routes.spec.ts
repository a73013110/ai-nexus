import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import {
  provideRouter,
  Router,
  UrlSegment,
  type ActivatedRouteSnapshot,
  type Route,
  type Routes,
  type UrlSegmentGroup,
  type UrlTree,
} from '@angular/router';
import { CATALOG_FEATURES } from '@core/api/feature-catalog';
import { catalogFeature } from '@core/api/catalog';
import { featureGuard } from '@core/auth/feature-guard';
import { routes } from './app.routes';

interface Collected {
  /** 由上層路徑累加的完整路徑；matcher 路由沒有自己的路徑，前綴就是上層路徑。 */
  segments: string[];
  route: Route;
  /** 同一層的其他路由，用來檢查 matcher 不會吃掉固定子路徑。 */
  siblings: Routes;
}

/** 展開所有 lazy 的 `loadChildren`，收集每一條路由與它的完整路徑。 */
async function collect(list: Routes, prefix: string[] = []): Promise<Collected[]> {
  const result: Collected[] = [];
  for (const route of list) {
    const segments = [...prefix, ...(route.path ?? '').split('/').filter(Boolean)];
    result.push({ segments, route, siblings: list.filter((item) => item !== route) });
    if (route.children) result.push(...(await collect(route.children, segments)));
    if (route.loadChildren) {
      const loaded = await route.loadChildren();
      expect(Array.isArray(loaded), `${segments.join('/')} 的 loadChildren 應回傳 Routes`).toBe(
        true,
      );
      result.push(...(await collect(loaded as Routes, segments)));
    }
  }
  return result;
}

const split = (url: string) => url.split('/').filter(Boolean);
const featureOf = (item: Collected) => item.route.data?.['feature'] as string | undefined;
/** 路由能否比對到目錄網址本身（matcher 路由以零個剩餘路徑段呼叫）。 */
function matchesBareUrl(item: Collected, url: string) {
  const { matcher, path } = item.route;
  if (matcher) return !!matcher([] as UrlSegment[], {} as UrlSegmentGroup, item.route);
  return item.segments.join('/') === split(url).join('/') && !path?.includes(':');
}

describe('routes and the feature catalog', () => {
  it('gives every catalog page and reader a route under its catalog URL', async () => {
    const all = await collect(routes);
    for (const feature of CATALOG_FEATURES) {
      if (feature.kind === 'action') continue;
      const matching = all.filter((item) => featureOf(item) === feature.id);
      expect(matching.length, `${feature.id} 沒有路由`).toBeGreaterThan(0);
      if (feature.kind === 'page')
        expect(
          matching.some((item) => matchesBareUrl(item, feature.route)),
          `${feature.id} 的路由比對不到 ${feature.route}`,
        ).toBe(true);
    }
  });

  it('points every routed feature at a catalog page or reader whose URL it lives under', async () => {
    for (const item of await collect(routes)) {
      const id = featureOf(item);
      if (id === undefined) continue;
      const feature = catalogFeature(id);
      expect(feature, `${id} 不在功能目錄中`).toBeDefined();
      expect(feature!.kind, `${id} 是動作，不能有路由`).not.toBe('action');
      const base = split(feature!.route!);
      expect(item.segments.slice(0, base.length), `${id} 的網址前綴`).toEqual(base);
      // 目錄網址之後只能是路由參數（閱讀器的 `:id`），不能另接固定路徑。
      for (const rest of item.segments.slice(base.length))
        expect(rest.startsWith(':'), `${id} 的 ${item.segments.join('/')}`).toBe(true);
      if (feature!.kind === 'reader')
        expect(item.segments.length, `${id} 要帶資源 ID`).toBeGreaterThan(base.length);
      expect(item.route.canMatch, `${id} 要掛 featureGuard`).toContain(featureGuard);
    }
  });

  it('keeps a matcher off the fixed paths of its siblings, so a guarded page is never opened as an id', async () => {
    for (const item of await collect(routes)) {
      const { matcher } = item.route;
      if (!matcher) continue;
      for (const sibling of item.siblings) {
        const first = sibling.path?.split('/')[0];
        if (!first || first.startsWith(':') || first === '**') continue;
        expect(
          matcher([new UrlSegment(first, {})], {} as UrlSegmentGroup, item.route),
          `${item.segments.join('/')} 的 matcher 會把 ${first} 當成 id`,
        ).toBeNull();
      }
    }
  });

  it('ends every module with its own fallback, so a guarded module root never renders blank', async () => {
    // 模組根網址沒有剩餘路徑段時，子路由全被守衛擋下仍算比對成功，不會退回區的 `**`（見 module-route.ts）。
    for (const item of await collect(routes)) {
      if (!item.route.loadChildren) continue;
      const loaded = (await item.route.loadChildren()) as Routes;
      if (!loaded.some((route) => route.data?.['feature'] !== undefined)) continue;
      expect(loaded.at(-1)?.path, `/${item.segments.join('/')} 的最後一條路由`).toBe('**');
    }
  });

  it('leaves only login, settings, the home redirect, area containers and fallbacks unmapped', async () => {
    for (const item of await collect(routes)) {
      if (featureOf(item) !== undefined) continue;
      const { route } = item;
      const path = item.segments.join('/');
      const container = !!(route.children || route.loadChildren);
      const allowed =
        container ||
        route.path === '**' ||
        (path === '' && route.redirectTo !== undefined) ||
        path === 'login' ||
        path === 'settings';
      expect(allowed, `沒有 data.feature 的路由：/${path}`).toBe(true);
    }
  });

  it('redirects the old staff area to office, keeping the rest of the path and the query', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const legacy = (await collect(routes)).find((item) => item.segments.join('/') === 'staff/**');
    const redirect = legacy?.route.redirectTo;
    expect(typeof redirect, '/staff/** 要轉址').toBe('function');
    const tree = TestBed.runInInjectionContext(() =>
      (redirect as (route: Partial<ActivatedRouteSnapshot>) => UrlTree)({
        url: [new UrlSegment('attendance', {}), new UrlSegment('records', {})],
        queryParams: { user: '7' },
      }),
    );
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe('/office/attendance/records?user=7');
  });
});
