import { computed, inject, Injectable, linkedSignal, signal } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { DashboardStore } from '../dashboard-store';
import {
  dashboardWidgets,
  isTileSize,
  type DashboardWidget,
  type TileSize,
} from '../widgets/dashboard-widgets';

/**
 * Tiles that no longer exist anywhere (the administrator-only sections and the service status
 * tile, which belong to the administration pages). Saved arrangements may still name them; they
 * are dropped on read so they neither show nor linger.
 */
const RETIRED = new Set(['section:monitoring', 'section:platform', 'services']);

/** A tile's place and shape as the viewer arranged it. */
export interface TileState {
  id: string;
  size: TileSize;
  tall: boolean;
  hidden: boolean;
}
export interface PlacedTile extends TileState {
  widget: DashboardWidget;
}

/** Reads a saved arrangement, dropping anything malformed instead of failing. */
export function parseTiles(value: unknown): TileState[] | null {
  const tiles = (value as { v?: number; tiles?: unknown } | null)?.tiles;
  if (!Array.isArray(tiles)) return null;
  return tiles
    .filter(
      (x): x is TileState =>
        !!x &&
        typeof x.id === 'string' &&
        x.id.length <= 120 &&
        !RETIRED.has(x.id) &&
        isTileSize(x.size),
    )
    .slice(0, 100)
    .map((x) => ({ id: x.id, size: x.size, tall: x.tall === true, hidden: x.hidden === true }));
}

/**
 * The saved arrangement with the widgets available now. Saved tiles keep their order (including
 * ones unavailable at the moment, so a feature granted again returns to its place); widgets the
 * viewer has never arranged, such as a new feature's section, are added at the end.
 */
export function mergeTiles(
  widgets: readonly DashboardWidget[],
  saved: readonly TileState[] | null,
) {
  const defaults = (widget: DashboardWidget): TileState => ({
    id: widget.id,
    size: widget.size,
    tall: !!widget.tall,
    hidden: false,
  });
  if (!saved) return widgets.map(defaults);
  const seen = new Set<string>();
  const tiles = saved.filter((tile) => !seen.has(tile.id) && !!seen.add(tile.id));
  return [...tiles, ...widgets.filter((widget) => !seen.has(widget.id)).map(defaults)];
}

/** Moves `id` to where `target` is, before it when moving up and after it when moving down. */
export function moveTile(tiles: readonly TileState[], id: string, target: string): TileState[] {
  const from = tiles.findIndex((x) => x.id === id),
    to = tiles.findIndex((x) => x.id === target);
  if (from < 0 || to < 0 || from === to) return [...tiles];
  const result = [...tiles];
  const [tile] = result.splice(from, 1);
  result.splice(to, 0, tile);
  return result;
}

/** Changes one tile's size, height or visibility; every size can follow every other. */
export function updateTile(
  tiles: readonly TileState[],
  id: string,
  change: Partial<Omit<TileState, 'id'>>,
): TileState[] {
  return tiles.map((x) => (x.id === id ? { ...x, ...change } : x));
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
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* A convenience only; storage can be disabled by policy. */
  }
}

/**
 * How this browser shows the overview for the signed-in account: per scope, which tiles appear in
 * what order and size. Presentation only; it never changes what is read. (A visual style saved by
 * earlier versions under `nexus.dashboard.style.*` is no longer read.)
 */
@Injectable()
export class DashboardLayout {
  private readonly store = inject(DashboardStore);
  private readonly session = inject(WorkspaceSession);
  private readonly user = computed(() => this.session.me()?.id ?? '');
  readonly editing = signal(false);
  readonly widgets = computed(() =>
    this.store.data()
      ? dashboardWidgets({ scope: this.store.scope(), sections: this.store.sections() })
      : [],
  );
  private readonly key = computed(
    () => `nexus.dashboard.layout.${this.user()}.${this.store.scope()}`,
  );
  private readonly saved = linkedSignal(() => parseTiles(read(this.key())));
  readonly tiles = computed(() => mergeTiles(this.widgets(), this.saved()));
  private readonly placed = computed(() => {
    const widgets = new Map(this.widgets().map((x) => [x.id, x]));
    return this.tiles()
      .filter((x) => widgets.has(x.id))
      .map((x): PlacedTile => ({ ...x, widget: widgets.get(x.id)! }));
  });
  readonly visible = computed(() => this.placed().filter((x) => !x.hidden));
  readonly hidden = computed(() => this.placed().filter((x) => x.hidden));
  /** The viewer arranged this scope; reset returns to the defaults. */
  readonly customized = computed(() => this.saved() !== null);

  move(id: string, target: string) {
    this.commit(moveTile(this.tiles(), id, target));
  }
  /** Moves a tile by `offset` places among the visible tiles. */
  shift(id: string, offset: number) {
    const visible = this.visible();
    const index = visible.findIndex((x) => x.id === id);
    const target = visible[Math.max(0, Math.min(visible.length - 1, index + offset))];
    if (index >= 0 && target) this.move(id, target.id);
  }
  resize(id: string, size: TileSize) {
    this.update(id, { size });
  }
  setTall(id: string, tall: boolean) {
    this.update(id, { tall });
  }
  setHidden(id: string, hidden: boolean) {
    this.update(id, { hidden });
  }
  reset() {
    this.saved.set(null);
    write(this.key(), null);
  }
  private update(id: string, change: Partial<Omit<TileState, 'id'>>) {
    this.commit(updateTile(this.tiles(), id, change));
  }
  private commit(tiles: TileState[]) {
    this.saved.set(tiles);
    write(this.key(), { v: 1, tiles });
  }
}
