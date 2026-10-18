import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { WorkspaceSession } from '../auth/workspace-session';
import { navigationOrder, navigationTree } from './navigation-tree';

export const PIN_LIMIT = 5;
export type NavigationDisplay = 'icons' | 'list';
const DEFAULT_PINS = ['dashboard', 'chat', 'knowledge', 'projects'];

/**
 * 側邊欄如何列出此帳號的功能：哪幾個釘在最上方，以及功能要顯示為圖示還是每行一個。
 * 兩者都只是瀏覽器偏好，只影響呈現、不影響權限；帳號已無法開啟的功能，其釘選會被略過而非移除。
 */
@Injectable({ providedIn: 'root' })
export class NavigationPreferences {
  private readonly session = inject(WorkspaceSession);
  /** 有頁面的功能；只授權、沒有路由的功能不會進入選單。 */
  readonly features = computed(() =>
    (this.session.me()?.access.features ?? []).filter((feature) => !!feature.route),
  );
  /** 管理員安排的選單（`navigationTree`），啟動器與 Ctrl+K 共用。 */
  readonly tree = computed(() =>
    navigationTree(this.features(), this.session.me()?.access.navigationGroups ?? []),
  );
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
  readonly ids = computed(() => {
    const visible = new Set(this.features().map((feature) => feature.id));
    // 沒有儲存的選擇時，釘選帳號能開啟的前四個：先依固定偏好，再依選單順序。
    const menu = navigationOrder(this.tree()).map((feature) => feature.id);
    const ids = this.saved() ?? [...new Set([...DEFAULT_PINS, ...menu])];
    return ids.filter((id) => visible.has(id)).slice(0, this.saved() ? PIN_LIMIT : 4);
  });
  readonly pinned = computed(() => {
    const features = new Map(this.features().map((feature) => [feature.id, feature]));
    return this.ids().map((id) => features.get(id)!);
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
