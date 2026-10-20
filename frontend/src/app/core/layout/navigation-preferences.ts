import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import type { NavigationNodeDto } from '@core/api/schema';
import { WorkspaceSession } from '../auth/workspace-session';
import { isGroup, navigationPages } from './navigation-tree';
import type { FeatureId } from '../api/catalog';

export const PIN_LIMIT = 5;
export type NavigationDisplay = 'icons' | 'list';
const DEFAULT_PINS: readonly FeatureId[] = ['chat', 'files', 'knowledge'];
/**
 * 第二層以下的分群超過這個數量就預設收合（窄螢幕不論數量都收合）。每個子分群至少一列標題加一列格子，
 * 超過 6 個時全部展開，1280×768 的桌面側欄一屏只看得到一兩個第一層分群，找不到自己在哪。
 */
export const DEFAULT_COLLAPSE_THRESHOLD = 6;
const NOTHING: ReadonlySet<string> = new Set();

/**
 * 側邊欄如何列出此帳號的功能：哪幾個釘在最上方、功能要顯示為圖示還是每行一個，以及收合了哪些分群。
 * 都只是瀏覽器偏好，只影響呈現、不影響權限；帳號已無法開啟的功能，其釘選會被略過而非移除。
 */
@Injectable({ providedIn: 'root' })
export class NavigationPreferences {
  private readonly session = inject(WorkspaceSession);
  /** 管理員安排、伺服器已依此帳號修剪的選單樹（`/me` 的 `navigation`），啟動器與 Ctrl+K 共用。 */
  readonly tree = computed(() => this.session.me()?.access.navigation ?? []);
  /**
   * 選單中的頁面（同一功能只算一次）：釘選的候選與「目前頁面」的判斷都來自這裡。
   * 已授權但管理員沒放進選單的頁面不能釘選，與側欄看不到它一致。
   */
  readonly pages = computed(() => navigationPages(this.tree()));
  private readonly user = computed(() => this.session.me()?.id ?? '');
  private readonly saved = linkedSignal(() => {
    const value = read(this.key('pins'));
    return Array.isArray(value) && value.every((id) => typeof id === 'string')
      ? value.slice(0, 20)
      : null;
  });
  readonly display = linkedSignal<NavigationDisplay>(() =>
    read(this.key('navigation-display')) === 'list' ? 'list' : 'icons',
  );
  /**
   * 已儲存的收合分群；`null` 表示從未儲存（與「已儲存的空集合＝全部展開」不同）。不在目前樹上的 ID
   * 不清掉：樹已依授權修剪，暫時看不到的分群在授權恢復後仍維持原本的收合狀態。
   */
  private readonly stored = linkedSignal<ReadonlySet<string> | null>(() => {
    const value = read(this.key('navigation-collapsed'));
    return Array.isArray(value)
      ? new Set(value.filter((id): id is string => typeof id === 'string'))
      : null;
  });
  /** 從未儲存時的預設收合；由呈現端提供，因為要看目前頁面與螢幕寬度（見 `defaultCollapsed`）。 */
  private readonly fallback = signal<() => ReadonlySet<string>>(() => NOTHING);
  /** 收合的分群（選單節點 ID）。第一次展開或收合時，以當下看到的狀態（含預設）存起來。 */
  readonly collapsed = computed(() => this.stored() ?? this.fallback()());
  readonly ids = computed(() => {
    const visible = new Set(this.pages().map((page) => page.id));
    // 沒有儲存的選擇時，只釘選帳號能開啟的預設功能，不從選單補滿；其他功能從啟動器開啟。
    return (this.saved() ?? DEFAULT_PINS).filter((id) => visible.has(id)).slice(0, PIN_LIMIT);
  });
  readonly pinned = computed(() => {
    const pages = new Map(this.pages().map((page) => [page.id, page]));
    return this.ids().map((id) => pages.get(id)!);
  });
  readonly full = computed(() => this.ids().length >= PIN_LIMIT);
  has(id: string) {
    return this.ids().includes(id);
  }
  toggle(id: string) {
    const base = this.saved() ?? this.ids();
    const ids = this.has(id)
      ? base.filter((item) => item !== id)
      : this.full()
        ? base
        : [...base.filter((item) => item !== id), id];
    this.saved.set(ids);
    write(this.key('pins'), ids);
  }
  isCollapsed(group: string) {
    return this.collapsed().has(group);
  }
  /** 展開或收合分群；`expand` 一次展開多個（目前頁面所在的分群）。沒有變化時不寫入。 */
  setCollapsed(group: string, collapsed: boolean) {
    if (this.isCollapsed(group) === collapsed) return;
    const next = new Set(this.collapsed());
    if (collapsed) next.add(group);
    else next.delete(group);
    this.saveCollapsed(next);
  }
  expand(groups: readonly string[]) {
    this.setAllCollapsed(groups, false);
  }
  /** 一次展開或收合多個分群（「全部展開／收合」）；沒有變化時不寫入。 */
  setAllCollapsed(groups: readonly string[], collapsed: boolean) {
    const current = this.collapsed();
    if (groups.every((group) => current.has(group) === collapsed)) return;
    const next = new Set(current);
    for (const group of groups) {
      if (collapsed) next.add(group);
      else next.delete(group);
    }
    this.saveCollapsed(next);
  }
  /** 指定從未儲存時的預設收合；只改預設，已儲存的偏好不受影響。 */
  useDefaultCollapsed(defaults: () => ReadonlySet<string>) {
    this.fallback.set(defaults);
  }
  private saveCollapsed(groups: ReadonlySet<string>) {
    this.stored.set(groups);
    write(this.key('navigation-collapsed'), [...groups]);
  }
  setDisplay(display: NavigationDisplay) {
    this.display.set(display);
    write(this.key('navigation-display'), display);
  }
  /** 偏好依帳號分開存放；在 computed／linkedSignal 中呼叫時會一起追蹤帳號。 */
  private key(name: 'pins' | 'navigation-display' | 'navigation-collapsed') {
    return `ai-nexus.${name}.${this.user()}`;
  }
}

/**
 * 從未儲存收合偏好時的預設：窄螢幕，或第二層以下（深度 ≥ 1）的分群超過門檻時，收合這些分群中
 * 不含目前頁面的，只留目前頁面所在的那一支；第一層一律展開，整體結構仍一眼看得到。
 * `current` 是目前頁面的所有上層分群。
 */
export function defaultCollapsed(
  tree: readonly NavigationNodeDto[],
  current: readonly string[],
  narrow: boolean,
): ReadonlySet<string> {
  const nested: string[] = [];
  const visit = (nodes: readonly NavigationNodeDto[], depth: number) => {
    for (const node of nodes) {
      if (!isGroup(node)) continue;
      if (depth > 0) nested.push(node.id);
      visit(node.children, depth + 1);
    }
  };
  visit(tree, 0);
  if (!narrow && nested.length <= DEFAULT_COLLAPSE_THRESHOLD) return NOTHING;
  return new Set(nested.filter((id) => !current.includes(id)));
}

function read(key: string): unknown {
  try {
    return JSON.parse(localStorage.getItem(key) ?? 'null');
  } catch {
    return null;
  }
}

function write(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* 只是便利功能；儲存可能被政策停用。 */
  }
}
