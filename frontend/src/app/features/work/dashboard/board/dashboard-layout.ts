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
 * 已不存在於任何地方的磚塊（僅限管理員的區段，以及服務狀態磚塊，它們屬於系統管理頁面）。
 * 已儲存的排列仍可能提到它們；讀取時會丟棄，讓它們既不顯示也不殘留。
 */
const RETIRED = new Set(['section:monitoring', 'section:platform', 'services']);

/** 檢視者排列後，磚塊的位置與形狀。 */
export interface TileState {
  id: string;
  size: TileSize;
  tall: boolean;
  hidden: boolean;
}
export interface PlacedTile extends TileState {
  widget: DashboardWidget;
}

/** 讀取已儲存的排列；格式錯誤的項目會被丟棄，不會失敗。 */
export function parseTiles(value: unknown): TileState[] | null {
  const tiles = (value as { v?: number; tiles?: unknown } | null)?.tiles;
  if (!Array.isArray(tiles)) return null;
  return tiles
    .filter(
      (item): item is TileState =>
        !!item &&
        typeof item.id === 'string' &&
        item.id.length <= 120 &&
        !RETIRED.has(item.id) &&
        isTileSize(item.size),
    )
    .slice(0, 100)
    .map((item) => ({
      id: item.id,
      size: item.size,
      tall: item.tall === true,
      hidden: item.hidden === true,
    }));
}

/**
 * 依目前可用的小工具，回傳已儲存的排列。已儲存的磚塊保留原順序（包括暫時不可用的，
 * 因此重新授權的功能會回到原位）；檢視者從未排列過的小工具（例如新功能的區段）則加在最後。
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

/** 把 `id` 移到 `target` 的位置：往上移時放在它之前，往下移時放在它之後。 */
export function moveTile(tiles: readonly TileState[], id: string, target: string): TileState[] {
  const from = tiles.findIndex((item) => item.id === id),
    to = tiles.findIndex((item) => item.id === target);
  if (from < 0 || to < 0 || from === to) return [...tiles];
  const result = [...tiles];
  const [tile] = result.splice(from, 1);
  result.splice(to, 0, tile);
  return result;
}

/** 改變一個磚塊的大小、高度或可見性；每種大小都可以接到其他大小。 */
export function updateTile(
  tiles: readonly TileState[],
  id: string,
  change: Partial<Omit<TileState, 'id'>>,
): TileState[] {
  return tiles.map((item) => (item.id === id ? { ...item, ...change } : item));
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
    /* 只是便利功能；儲存可能被政策停用。 */
  }
}

/**
 * 此瀏覽器如何為已登入的帳號顯示總覽：依範圍決定哪些磚塊以什麼順序與大小出現。
 * 只影響呈現，不影響讀取的內容。
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
    () => `ai-nexus.dashboard.layout.${this.user()}.${this.store.scope()}`,
  );
  private readonly saved = linkedSignal(() => parseTiles(read(this.key())));
  readonly tiles = computed(() => mergeTiles(this.widgets(), this.saved()));
  private readonly placed = computed(() => {
    const widgets = new Map(this.widgets().map((item) => [item.id, item]));
    return this.tiles()
      .filter((item) => widgets.has(item.id))
      .map((item): PlacedTile => ({ ...item, widget: widgets.get(item.id)! }));
  });
  readonly visible = computed(() => this.placed().filter((item) => !item.hidden));
  readonly hidden = computed(() => this.placed().filter((item) => item.hidden));
  /** 檢視者為此範圍做過排列；重設會回到預設值。 */
  readonly customized = computed(() => this.saved() !== null);

  move(id: string, target: string) {
    this.commit(moveTile(this.tiles(), id, target));
  }
  /** 在可見磚塊之間，把磚塊往後移 `offset` 個位置。 */
  shift(id: string, offset: number) {
    const visible = this.visible();
    const index = visible.findIndex((item) => item.id === id);
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
