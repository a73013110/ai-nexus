import {
  CATALOG_AREAS,
  CATALOG_FEATURES,
  CATALOG_MODULES,
  type CatalogArea,
  type CatalogFeature,
  type CatalogId,
  type CatalogModule,
} from './feature-catalog';
import type { FeatureId, NavigationSectionDto } from './schema';

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

/** 依選單位置分成的一組功能（`featureSections`）。 */
export interface FeatureSection<T> {
  id: string;
  /** 分群路徑（如「系統管理 › 身分與權限」），或「最上層」「未放入選單」「其他」。 */
  name: string;
  features: T[];
}

/**
 * 把功能照側欄的位置分組（授權摘要、群組的功能勾選、稽核的功能異動共用），讓管理員在授權清單看到的
 * 和側欄一致：`navigation` 是伺服器依「完整」選單算出的位置（管理與稽核目錄的 `navigationSections`），
 * 組與組內依選單順序，動作已排在上層頁面之後。不能改用 `/me` 的選單：那棵樹依檢視者的權限修剪過，
 * 分組別人的授權時會漏掉位置。沒放進選單的功能放在倒數第二組（目錄順序），不在目錄中的 ID
 * （舊資料、已移除的功能）放在最後一組，照原樣顯示；不論輸入順序（稽核快照的 ID 可能依字母排序）。
 */
export function featureSections<T extends { id: string }>(
  features: readonly T[],
  navigation: readonly NavigationSectionDto[],
): FeatureSection<T>[] {
  const sections = navigation.map((section) => ({
    id: section.id ? 'menu:' + section.id : 'menu',
    name: section.path.length ? section.path.join(' › ') : '最上層',
    features: [] as T[],
  }));
  const unplaced: FeatureSection<T> = { id: 'unplaced', name: '未放入選單', features: [] };
  const unknown: FeatureSection<T> = { id: 'unknown', name: '其他', features: [] };
  const positions = new Map<string, [section: number, order: number]>();
  navigation.forEach((section, index) =>
    section.featureIds.forEach((id, order) => {
      if (!positions.has(id)) positions.set(id, [index, order]);
    }),
  );
  const ordered = [...features].sort(
    (first, second) =>
      (positions.get(first.id)?.[1] ?? catalogOrder(first.id)) -
      (positions.get(second.id)?.[1] ?? catalogOrder(second.id)),
  );
  for (const feature of ordered) {
    const position = positions.get(feature.id);
    if (position) sections[position[0]].features.push(feature);
    else (catalogFeature(feature.id) ? unplaced : unknown).features.push(feature);
  }
  return [...sections, unplaced, unknown].filter((section) => section.features.length);
}

const featureOrder = new Map<string, number>(
  CATALOG_FEATURES.map((item, index) => [item.id, index]),
);
function catalogOrder(id: string) {
  return featureOrder.get(id) ?? Number.MAX_SAFE_INTEGER;
}
