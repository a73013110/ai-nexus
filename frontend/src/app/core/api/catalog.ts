import {
  CATALOG_AREAS,
  CATALOG_FEATURES,
  CATALOG_MODULES,
  type CatalogArea,
  type CatalogFeature,
  type CatalogId,
  type CatalogModule,
} from './feature-catalog';
import type { FeatureId } from './schema';

export type { CatalogFeature, CatalogId, FeatureId };
/** 有網址的目錄項目：頁面要授權，閱讀器只需登入（能不能讀由資源 ACL 決定）。 */
export type RoutedFeature = Extract<CatalogFeature, { kind: 'page' | 'reader' }>;
export type RoutedId = RoutedFeature['id'];
export type PageId = Extract<CatalogFeature, { kind: 'page' }>['id'];

const features = new Map<string, CatalogFeature>(CATALOG_FEATURES.map((item) => [item.id, item]));
const modules = new Map<string, CatalogModule>(CATALOG_MODULES.map((item) => [item.id, item]));
const areas = new Map<string, CatalogArea>(CATALOG_AREAS.map((item) => [item.id, item]));
const routed = CATALOG_FEATURES.filter((item): item is RoutedFeature => item.route !== null).map(
  (item) => ({ feature: item, segments: item.route.split('/').filter(Boolean) }),
);

/** 目錄中的頁面、動作或閱讀器；API 給的任意字串（稽核、監控、舊資料）可能不在其中。 */
export function catalogFeature(id: string): CatalogFeature | undefined {
  return features.get(id);
}

/** 可授予（頁面＋動作）的 ID，與 `/me` 與管理端的 grant 同一套；閱讀器不是授權。 */
export function isFeatureId(id: string): id is FeatureId {
  const feature = features.get(id);
  return !!feature && feature.kind !== 'reader';
}

/** 頁面或閱讀器的網址；前端連結一律由此取得，不寫字面值，模組換區時跟著目錄走。 */
export function routeOf(id: RoutedId): string {
  return (features.get(id) as RoutedFeature).route;
}

export function catalogModule(id: string): CatalogModule | undefined {
  return modules.get(id);
}

export function catalogArea(id: string): CatalogArea | undefined {
  return areas.get(id);
}

/** 同一模組的頁面，依目錄順序；給模組內導覽用。 */
export function modulePages(module: string) {
  return CATALOG_FEATURES.filter(
    (item): item is Extract<CatalogFeature, { kind: 'page' }> =>
      item.kind === 'page' && item.module === module,
  );
}

/**
 * 網址所屬的頁面或閱讀器：以整段比對目錄網址，取最長的前綴（`/work/knowledge/retrieval` 屬於檢索管理，
 * `/work/knowledge/<id>` 屬於知識庫）。查詢字串、片段與矩陣參數不參與比對；不在目錄中時為 `undefined`。
 */
export function featureForUrl(url: string): RoutedFeature | undefined {
  const path = url.split(/[?#]/)[0];
  const segments = path
    .split('/')
    .filter(Boolean)
    .map((segment) => segment.split(';')[0]);
  let best: RoutedFeature | undefined;
  let depth = 0;
  for (const candidate of routed) {
    if (
      candidate.segments.length > depth &&
      candidate.segments.length <= segments.length &&
      candidate.segments.every((segment, index) => segments[index] === segment)
    ) {
      best = candidate.feature;
      depth = candidate.segments.length;
    }
  }
  return best;
}
