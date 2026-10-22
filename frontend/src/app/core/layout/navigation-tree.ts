import type { NavigationNodeDto } from '@core/api/schema';
import { ICON_NAMES } from '@shared/ui/icon';

/** 選單節點的種類；與後端 `NavigationItem` 的 Kind 相同。 */
export type NavigationKind = 'group' | 'page' | 'link';

/**
 * 一個分群（或最上層，`id` 為空字串）扁平化成一個區段，供不巢狀的列表（Ctrl+K）使用。
 * `entries` 只有直接放在這層的頁面與外部連結；子分群是自己的區段。
 */
export interface NavigationSection {
  id: string;
  /** 分群名稱；最上層則為 `topName`。 */
  name: string;
  /** 祖先名稱加上分群自己的名稱；扁平列表仍要能分辨巢狀分群。 */
  path: string;
  depth: number;
  entries: NavigationNodeDto[];
}

/** 一層子節點依原順序切成連續的項目（頁面、連結）與分群，啟動器才能把相鄰項目排成同一格狀區塊。 */
export type NavigationRun =
  | { kind: 'entries'; key: string; nodes: NavigationNodeDto[] }
  | { kind: 'group'; key: string; node: NavigationNodeDto };

/** 選單中可以開啟的頁面（釘選的單位仍是功能 ID）。 */
export interface NavigationPage {
  id: string;
  name: string;
  route: string;
  icon: string;
}

export const isGroup = (node: NavigationNodeDto) => node.kind === 'group';

/**
 * 依選單順序（深度優先）回傳區段。`/me` 的 `navigation` 已由伺服器修剪（未授權的頁面、看不到的連結、
 * 空分群都已移除；只剩一項的分群仍保留），這裡只負責排成扁平列表，不重新判斷可見性。
 * 第一個區段是最上層，名稱為 `topName`；沒有項目時會省略，除非 `keepTop`。
 */
export function navigationSections(
  nodes: readonly NavigationNodeDto[],
  { topName = '', keepTop = false } = {},
): NavigationSection[] {
  const sections: NavigationSection[] = [];
  const top = nodes.filter((node) => !isGroup(node));
  if (keepTop || top.length)
    sections.push({ id: '', name: topName, path: topName, depth: 0, entries: top });
  const visit = (node: NavigationNodeDto, depth: number, trail: string[]) => {
    const path = [...trail, node.name];
    sections.push({
      id: node.id,
      name: node.name,
      path: path.join(' › '),
      depth,
      entries: node.children.filter((child) => !isGroup(child)),
    });
    for (const child of node.children) if (isGroup(child)) visit(child, depth + 1, path);
  };
  for (const node of nodes) if (isGroup(node)) visit(node, 0, []);
  return sections;
}

/** 把一層子節點切成連續項目與分群（見 `NavigationRun`），不改變順序。 */
export function navigationRuns(nodes: readonly NavigationNodeDto[]): NavigationRun[] {
  const runs: NavigationRun[] = [];
  for (const node of nodes) {
    const last = runs.at(-1);
    if (isGroup(node)) runs.push({ kind: 'group', key: node.id, node });
    else if (last?.kind === 'entries') last.nodes.push(node);
    else runs.push({ kind: 'entries', key: 'entries:' + node.id, nodes: [node] });
  }
  return runs;
}

/** 深度優先的每個節點；Ctrl+K 執行指令時由節點 ID 找回它。 */
export function navigationNodes(nodes: readonly NavigationNodeDto[]): NavigationNodeDto[] {
  return nodes.flatMap((node) => [node, ...navigationNodes(node.children)]);
}

/**
 * 選單中的頁面，同一功能只取第一次出現的位置。管理員可以把同一頁面放在不同分群，
 * 但釘選與「目前頁面」都以功能為單位，只能對應一個項目。
 */
export function navigationPages(nodes: readonly NavigationNodeDto[]): NavigationPage[] {
  const pages = new Map<string, NavigationPage>();
  for (const node of navigationNodes(nodes))
    if (node.kind === 'page' && node.featureId && node.route && !pages.has(node.featureId))
      pages.set(node.featureId, {
        id: node.featureId,
        name: node.name,
        route: node.route,
        icon: node.icon ?? '',
      });
  return [...pages.values()];
}

/** API 給的圖示名稱前端認得時原樣使用，否則為 `null`（由呼叫端決定備用圖示）。 */
const knownIcon = (icon: string | null | undefined) => (icon && ICON_NAMES.has(icon) ? icon : null);

/** 選單能繪製的圖示名稱：已知時用 API 的值，否則用一個中性的備用圖示。 */
export function featureIcon(icon: string | null | undefined): string {
  return knownIcon(icon) ?? 'document';
}

/**
 * 節點的圖示：頁面用目錄圖示；外部連結沒有設定（或前端不認得）時用外部連結圖示，
 * 讓它即使只顯示圖示也看得出會離開本站；分群沒有設定時不顯示圖示（`null`）。
 */
export function navigationIcon(node: NavigationNodeDto): string | null {
  if (node.kind === 'page') return featureIcon(node.icon);
  const known = knownIcon(node.icon);
  return node.kind === 'link' ? (known ?? 'external') : known;
}
