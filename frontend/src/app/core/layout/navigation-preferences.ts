import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { WorkspaceSession } from '../auth/workspace-session';
import { navigationPages } from './navigation-tree';
import type { FeatureId } from '../api/catalog';

export const PIN_LIMIT = 5;
export type NavigationDisplay = 'icons' | 'list';
const DEFAULT_PINS: readonly FeatureId[] = ['chat', 'files', 'knowledge'];

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
    const value = read(`ai-nexus.pins.${this.user()}`);
    return Array.isArray(value) && value.every((id) => typeof id === 'string')
      ? value.slice(0, 20)
      : null;
  });
  readonly display = linkedSignal<NavigationDisplay>(() =>
    read(`ai-nexus.navigation-display.${this.user()}`) === 'list' ? 'list' : 'icons',
  );
  /**
   * 收合的分群（選單節點 ID）；預設全部展開。不在目前樹上的 ID 不清掉：樹已依授權修剪，
   * 暫時看不到的分群在授權恢復後仍維持原本的收合狀態。
   */
  readonly collapsed = linkedSignal<ReadonlySet<string>>(() => {
    const value = read(`ai-nexus.navigation-collapsed.${this.user()}`);
    return new Set(
      Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : [],
    );
  });
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
    write(`ai-nexus.pins.${this.user()}`, ids);
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
    const current = this.collapsed();
    if (!groups.some((group) => current.has(group))) return;
    this.saveCollapsed(new Set([...current].filter((group) => !groups.includes(group))));
  }
  private saveCollapsed(groups: ReadonlySet<string>) {
    this.collapsed.set(groups);
    write(`ai-nexus.navigation-collapsed.${this.user()}`, [...groups]);
  }
  setDisplay(display: NavigationDisplay) {
    this.display.set(display);
    write(`ai-nexus.navigation-display.${this.user()}`, display);
  }
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
