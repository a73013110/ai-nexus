import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { WorkspaceSession } from '../auth/workspace-session';
import { navigationOrder, navigationTree } from './navigation-tree';

export const PIN_LIMIT = 5;
export type NavigationDisplay = 'icons' | 'list';
const DEFAULT_PINS = ['dashboard', 'chat', 'knowledge', 'projects'];

/**
 * How the sidebar lists features for this account: which few are pinned on top and whether
 * features show as icons or one per line. Both are browser preferences that only change
 * presentation, never access, so pins for features the account can no longer open are skipped
 * rather than removed.
 */
@Injectable({ providedIn: 'root' })
export class NavigationPreferences {
  private readonly session = inject(WorkspaceSession);
  /** Features with a page; grant-only features (no route) never reach the menu. */
  readonly features = computed(() =>
    (this.session.me()?.access.features ?? []).filter((feature) => !!feature.route),
  );
  /** The menu as administrators arranged it (`navigationTree`), shared by the launcher and Ctrl+K. */
  readonly tree = computed(() =>
    navigationTree(this.features(), this.session.me()?.access.navigationGroups ?? []),
  );
  private readonly user = computed(() => this.session.me()?.id ?? '');
  private readonly saved = linkedSignal(() => {
    const value = read(`nexus.pins.${this.user()}`);
    return Array.isArray(value) && value.every((id) => typeof id === 'string')
      ? value.slice(0, 20)
      : null;
  });
  readonly display = linkedSignal<NavigationDisplay>(() =>
    read(`nexus.navigation-display.${this.user()}`) === 'list' ? 'list' : 'icons',
  );
  readonly ids = computed(() => {
    const visible = new Set(this.features().map((feature) => feature.id));
    // Without a saved choice, pin the first four the account can open: a fixed preference, then menu order.
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
      ? base.filter((x) => x !== id)
      : this.full()
        ? base
        : [...base.filter((x) => x !== id), id];
    this.saved.set(ids);
    write(`nexus.pins.${this.user()}`, ids);
  }
  setDisplay(display: NavigationDisplay) {
    this.display.set(display);
    write(`nexus.navigation-display.${this.user()}`, display);
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
    /* A convenience only; storage can be disabled by policy. */
  }
}
