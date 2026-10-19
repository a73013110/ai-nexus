import type { NavigationNodeDto } from '@core/api/schema';
import { catalogFeature, catalogModule } from '@core/api/catalog';
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
export const isLink = (node: NavigationNodeDto) => node.kind === 'link';

/**
 * 依選單順序（深度優先）回傳區段。`/me` 的 `navigation` 已由伺服器修剪（未授權的頁面、看不到的連結、
 * 空分群都已移除，只剩一個子節點的分群已攤平），這裡只負責排成扁平列表，不重新判斷可見性。
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

/**
 * 有自己模組內導覽的模組：存取控制有頁面間的分頁列（`ModuleNavigation`），品質評測由列表頁進入檢索評測。
 * 只有這些模組能把多個頁面合成一個側欄入口；其他模組合併後，非第一頁就沒有入口了。
 */
export const SELF_NAVIGATING_MODULES: ReadonlySet<string> = new Set(['access-control', 'quality']);

/** 啟動器的一格：一般頁面或連結的 `members` 只有自己；模組入口的 `members` 是被合併的頁面節點。 */
export interface NavigationEntry {
  node: NavigationNodeDto;
  members: NavigationNodeDto[];
}

/**
 * 同一層裡屬於同一個 `SELF_NAVIGATING_MODULES` 模組的頁面，合成一個以模組命名、指向第一個頁面的入口，
 * 放在第一個頁面的位置（ADR 0016：模組內的頁面用模組內導覽，側欄不逐頁列出）。只剩一頁時照原樣顯示頁名。
 * 只改呈現：Ctrl+K、釘選與「目前頁面」仍以各頁面為單位。
 */
export function navigationEntries(nodes: readonly NavigationNodeDto[]): NavigationEntry[] {
  const entries: (NavigationEntry & { module?: string })[] = [];
  const bundles = new Map<string, NavigationEntry>();
  for (const node of nodes) {
    const module = node.kind === 'page' ? catalogFeature(node.featureId ?? '')?.module : undefined;
    const bundled = module && SELF_NAVIGATING_MODULES.has(module) ? module : undefined;
    const bundle = bundled && bundles.get(bundled);
    if (bundle) {
      bundle.members.push(node);
      continue;
    }
    const entry = { node, members: [node], module: bundled };
    entries.push(entry);
    if (bundled) bundles.set(bundled, entry);
  }
  return entries.map(({ node, members, module }) =>
    module && members.length > 1
      ? {
          node: { ...node, id: 'module:' + module, name: catalogModule(module)?.name ?? node.name },
          members,
        }
      : { node, members },
  );
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

/** 選單能繪製的圖示名稱：已知時用 API 的值，否則用一個中性的備用圖示。 */
export function featureIcon(icon: string | null | undefined): string {
  return icon && ICON_NAMES.has(icon) ? icon : 'document';
}

/**
 * 節點的圖示：頁面用目錄圖示；外部連結沒有設定（或前端不認得）時用外部連結圖示，
 * 讓它即使只顯示圖示也看得出會離開本站；分群沒有設定時不顯示圖示（`null`）。
 */
export function navigationIcon(node: NavigationNodeDto): string | null {
  if (node.kind === 'page') return featureIcon(node.icon);
  const known = node.icon && ICON_NAMES.has(node.icon) ? node.icon : null;
  return node.kind === 'link' ? (known ?? 'external') : known;
}
