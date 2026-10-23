import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type {
  DirectoryNodeDto,
  DirectoryPersonDetailDto,
  DirectoryPersonDto,
} from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import type { TreeItem } from '@shared/ui/tree-view';
import { IdentityApi } from '../identity-api';
import {
  DIRECTORY_BATCH_LIMIT,
  type DirectoryGroup,
  type DirectorySelection,
  groupOf,
  selectable,
  selectPeople,
  selectionState,
  withinDn,
} from './directory-outcomes';

/** 樹上一個已讀到的節點；`children` 是下一層的 DN，undefined 表示還沒展開過。 */
interface NodeState {
  node: DirectoryNodeDto;
  parent: string | null;
  children?: readonly string[];
  /** 子節點超過伺服器上限，只讀到前面一段。 */
  more?: boolean;
  loading: boolean;
}

type NodeMap = Map<string, NodeState>;

export type PeopleFilter = DirectoryGroup | 'all';
export type SidePanel = 'basket' | 'attributes';

const collator = new Intl.Collator('zh-Hant-TW');
/** 停在一個人身上多久才讀屬性：方向鍵連續移動時只讀最後停下的那位。 */
const DETAIL_DELAY = 250;
/** 節點超過上限時，搜尋停止輸入多久才到目錄端找人。 */
const SEARCH_DELAY = 350;
const icons: Record<string, string> = { root: 'globe', ou: 'folder', container: 'container' };

/** 把一個節點與它的直屬子節點寫進 `map`（就地修改並回傳）；子節點依中文排序。 */
function attach(
  map: NodeMap,
  dn: string,
  parent: string | null,
  result: { node: DirectoryNodeDto; children: readonly DirectoryNodeDto[]; moreChildren: boolean },
) {
  const sorted = [...result.children].sort((a, b) => collator.compare(a.name, b.name));
  map.set(dn, {
    node: result.node,
    parent,
    children: sorted.map((child) => child.dn),
    more: result.moreChildren,
    loading: false,
  });
  for (const child of sorted)
    map.set(child.dn, { ...map.get(child.dn), node: child, parent: dn, loading: false });
  return map;
}

/** DN 的上一層（第一個未跳脫的逗號之後）；人員所在的節點。 */
export function parentDn(dn: string) {
  for (let i = 0; i < dn.length; i++) {
    if (dn[i] === '\\') i++;
    else if (dn[i] === ',') return dn.slice(i + 1);
  }
  return '';
}

/**
 * AD 匯入對話框「選取人員」步驟的狀態，由對話框提供、三個窗格共用：目錄樹（逐層延遲載入）、
 * 目前節點的人員與篩選、跨節點累積的選取，以及右側面板。每次開啟（`open`）從頭開始；
 * 關閉或重新整理後才回來的節點回應以 `visit` 丟棄，不會寫進新的樹。
 * 人員的完整 AD 屬性在檢視時才讀（清單只帶匯入要的欄位），停留片刻才送出、讀過的留在這次開啟的快取，
 * 用方向鍵逐列瀏覽時不會每一列都打一次目錄。
 */
@Injectable()
export class DirectoryImportStore {
  private readonly api = inject(IdentityApi);
  readonly limit = DIRECTORY_BATCH_LIMIT;
  readonly opened = signal(false);
  private visit = 0;

  readonly nodes = signal<ReadonlyMap<string, NodeState>>(new Map());
  readonly root = signal<string | null>(null);
  readonly expanded = signal<ReadonlySet<string>>(new Set());
  readonly node = signal<string | null>(null);
  readonly treeError = signal('');
  readonly refreshing = signal(false);
  /** 服務帳號能否寫回 AD；目前一律唯讀，介面只預留位置。 */
  readonly writable = signal(false);

  readonly subtree = signal(false);
  readonly filter = signal<PeopleFilter>('all');
  readonly query = signal('');
  readonly hideDisabled = signal(false);
  readonly selection = signal<DirectorySelection>(new Map());
  readonly limitNote = signal('');
  /** 超過上限的節點（`dn|subtree`）：搜尋改到目錄端找人，否則上限之後的人永遠看不到。 */
  private readonly capped = signal<ReadonlySet<string>>(new Set());
  /** 送到目錄端的搜尋文字；停止輸入一下才跟上 `query`。 */
  private readonly serverQuery = signal('');
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  /** Shift 連續選取的起點：上一次點選的人。換節點或篩選時清掉，避免跨清單選到看不到的人。 */
  private anchor: string | null = null;
  readonly inspected = signal<string | null>(null);
  readonly panel = signal<SidePanel>('basket');
  /** 實際要讀屬性的人：`inspected` 停留 `DETAIL_DELAY` 毫秒後才跟上。 */
  private readonly detailSid = signal<string | null>(null);
  private details = new Map<string, DirectoryPersonDetailDto>();
  private detailTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // 選取當下的資料換成清單最新的值（換節點回來後狀態可能變了，例如剛匯入過），匯入籃的分組才會跟著更新。
    effect(() => {
      const people = this.all();
      untracked(() => this.refreshSelection(people));
    });
  }

  // 條件沒變就回傳同一個物件：記下「超過上限」時不能讓清單重讀，否則讀完又記、記了又讀。
  private readonly peopleParams = computed(
    () => {
      const node = this.node();
      if (!this.opened() || !node) return undefined;
      const subtree = this.subtree();
      const q = this.capped().has(`${node}|${subtree}`) ? this.serverQuery().trim() : '';
      return { node, subtree, q };
    },
    {
      equal: (a, b) => a?.node === b?.node && a?.subtree === b?.subtree && a?.q === b?.q,
    },
  );
  readonly people = apiResource({
    feature: 'identity.users.import',
    params: () => this.peopleParams(),
    // 回應帶上查詢條件：換節點時舊清單仍是 value，靠它判斷是不是目前節點的資料。
    loader: async (params, signal) => {
      const result = await this.api.directoryPeople(params.node, params.subtree, params.q, signal);
      const key = `${params.node}|${params.subtree}`;
      if (result.more && !params.q && !this.capped().has(key))
        this.capped.update((set) => new Set([...set, key]));
      return { ...params, ...result };
    },
  });
  /** 回應屬於目前的條件；換節點後、新清單回來前為 false，畫面顯示骨架而不是上一個節點的人。 */
  readonly listed = computed(() => {
    const value = this.people.value(),
      params = this.peopleParams();
    return (
      !!value &&
      !!params &&
      value.node === params.node &&
      value.subtree === params.subtree &&
      value.q === params.q
    );
  });
  /** 清單在讀取中（含還沒有節點可讀）；讀取失敗時為 false，表格才能顯示錯誤與重試。 */
  readonly listLoading = computed(() =>
    this.node() ? !this.listed() && !this.people.error() : !this.treeError(),
  );
  /** 目錄端搜尋的結果（節點超過上限、有輸入關鍵字）。 */
  readonly searchedServer = computed(() => this.listed() && !!this.people.value()?.q);
  readonly detail = apiResource({
    feature: 'identity.users.import',
    params: () => this.detailSid() ?? undefined,
    loader: async (sid, signal) => {
      const cached = this.details.get(sid);
      if (cached) return cached;
      const detail = await this.api.directoryPerson(sid, signal);
      this.details.set(sid, detail);
      return detail;
    },
  });
  /** 目前這個人的屬性讀取失敗；上一位的錯誤不會留在下一位身上。 */
  readonly detailError = computed(() =>
    this.detailSid() === this.inspected() ? this.detail.error() : undefined,
  );

  readonly current = computed(() => this.nodes().get(this.node() ?? '')?.node ?? null);
  /** 根到目前節點的路徑（麵包屑）；只用已讀到的節點，選到的節點一定已在樹上。 */
  readonly crumbs = computed(() => {
    const nodes = this.nodes(),
      chain: DirectoryNodeDto[] = [];
    for (let dn = this.node(); dn; dn = nodes.get(dn)?.parent ?? null) {
      const state = nodes.get(dn);
      if (!state) break;
      chain.unshift(state.node);
    }
    return chain;
  });

  readonly treeItems = computed<TreeItem[]>(() => {
    const nodes = this.nodes(),
      root = this.root(),
      selected = [...this.selection().values()];
    const build = (dn: string): TreeItem | null => {
      const state = nodes.get(dn);
      if (!state) return null;
      const { node } = state;
      const picked = selected.filter((person) => withinDn(person.dn, dn)).length;
      return {
        id: dn,
        label: node.name,
        icon: icons[node.kind] ?? 'folder',
        title: dn,
        count: node.people,
        countLabel: node.people === null ? undefined : `含下層 ${node.people} 人`,
        badge: picked,
        badgeLabel: picked ? `已選 ${picked} 人` : undefined,
        muted: node.people === 0,
        hasChildren: node.hasChildren,
        loading: state.loading,
        children: state.children?.map(build).filter((item): item is TreeItem => item !== null),
      };
    };
    const item = root ? build(root) : null;
    return item ? [item] : [];
  });

  /** 子節點被截斷的展開節點（樹下方提示）。 */
  readonly truncated = computed(() =>
    [...this.expanded()].flatMap((dn) => {
      const state = this.nodes().get(dn);
      return state?.more ? [state.node.name] : [];
    }),
  );

  /** 依中文排序（伺服器按 Unicode 排，筆畫順序對管理員比較好找）；Shift 連續選取也依這個順序。 */
  readonly all = computed(() =>
    [...((this.listed() && this.people.value()?.people) || [])].sort(
      (a, b) =>
        collator.compare(a.displayName, b.displayName) || collator.compare(a.account, b.account),
    ),
  );
  readonly more = computed(() => this.listed() && !!this.people.value()?.more);
  /** 搜尋與「隱藏 AD 停用」之後、狀態篩選之前的清單；篩選鈕的人數依它計算。 */
  private readonly matched = computed(() => {
    const words = this.query().trim().toLowerCase();
    const hide = this.hideDisabled();
    return this.all().filter(
      (person) =>
        !(hide && person.disabled) &&
        (!words ||
          [
            person.displayName,
            person.account,
            person.email,
            person.upn,
            person.department,
            person.title,
          ].some((value) => value?.toLowerCase().includes(words))),
    );
  });
  readonly counts = computed(() => {
    const counts: Record<PeopleFilter, number> = {
      all: 0,
      new: 0,
      changed: 0,
      synced: 0,
      blocked: 0,
    };
    for (const person of this.matched()) {
      counts.all++;
      counts[groupOf(person)]++;
    }
    return counts;
  });
  readonly visible = computed(() => {
    const filter = this.filter();
    return filter === 'all'
      ? this.matched()
      : this.matched().filter((person) => groupOf(person) === filter);
  });
  readonly headState = computed(() => selectionState(this.selection(), this.visible()));
  /** 這個節點清單裡已選的人數（與篩選無關）。 */
  readonly pickedHere = computed(
    () => this.all().filter((person) => this.selection().has(person.sid)).length,
  );

  /**
   * 表頭全選後的提示：清單裡若有與平台一致（匯入只會略過）的人，提供「只留需要處理的人」；
   * 否則讓管理員一鍵取消。只有一人時不提示。
   */
  readonly banner = computed(() => {
    if (this.headState() !== 'all') return null;
    const picked = this.visible().filter((person) => this.selection().has(person.sid));
    if (picked.length < 2) return null;
    const synced = picked.filter((person) => person.state === 'synced');
    return synced.length && synced.length < picked.length
      ? {
          text: `已選取這份清單的 ${picked.length} 人，其中 ${synced.length} 人與平台一致，匯入時會略過。`,
          action: `只留需要處理的 ${picked.length - synced.length} 人`,
          drop: synced,
        }
      : { text: `已選取這份清單的全部 ${picked.length} 人。`, action: '取消選取', drop: picked };
  });

  readonly selected = computed(() => [...this.selection().values()]);
  /** 已選的人來自幾個節點（人員的上一層 DN）。 */
  readonly sources = computed(
    () => new Set(this.selected().map((person) => parentDn(person.dn).toLowerCase())).size,
  );
  readonly basket = computed(() => {
    const groups: Record<'new' | 'changed' | 'synced', DirectoryPersonDto[]> = {
      new: [],
      changed: [],
      synced: [],
    };
    for (const person of this.selected()) {
      const group = groupOf(person);
      if (group !== 'blocked') groups[group].push(person);
    }
    return groups;
  });
  /** 目前檢視的人的完整屬性；換人後、新的回來前為 null，不會把上一位的屬性接在新的人底下。 */
  readonly inspectedDetail = computed(() => {
    const detail = this.detail.value();
    return detail && detail.sid === this.inspected() ? detail : null;
  });
  /** 右側「AD 屬性」的人：優先用目前清單的最新資料，換節點後退回選取當下的資料。 */
  readonly inspectedPerson = computed(() => {
    const sid = this.inspected();
    if (!sid) return null;
    return this.all().find((person) => person.sid === sid) ?? this.selection().get(sid) ?? null;
  });

  open() {
    this.visit++;
    this.refreshing.set(false);
    this.capped.set(new Set());
    this.nodes.set(new Map());
    this.root.set(null);
    this.expanded.set(new Set());
    this.node.set(null);
    this.treeError.set('');
    this.writable.set(false);
    this.subtree.set(false);
    this.resetList();
    this.selection.set(new Map());
    this.limitNote.set('');
    this.inspect(null);
    this.details = new Map();
    this.panel.set('basket');
    this.opened.set(true);
    void this.loadRoot(true);
  }
  close() {
    this.visit++;
    this.opened.set(false);
    this.refreshing.set(false);
    this.inspect(null);
    clearTimeout(this.searchTimer);
  }

  /**
   * 重讀整棵樹：在畫面外依層讀完再一次換上，讀的期間舊樹照常顯示、不跳動；保留展開與目前節點，
   * 消失的節點就收合，目前節點消失時改選根。重讀中再按不會重複送出。
   */
  async refresh() {
    if (this.refreshing()) return;
    const visit = ++this.visit;
    this.refreshing.set(true);
    this.treeError.set('');
    try {
      const result = await this.api.directoryNodes(undefined);
      if (visit !== this.visit) return;
      const next: NodeMap = new Map();
      const rootDn = result.node.dn;
      attach(next, rootDn, null, result);
      const expanded = this.expanded();
      // 由上往下：只有已讀到的節點才知道它的子節點，所以一層一層排進佇列。
      const queue = [...(next.get(rootDn)?.children ?? [])].filter((dn) => expanded.has(dn));
      while (queue.length) {
        const dn = queue.shift()!;
        try {
          const branch = await this.api.directoryNodes(dn);
          if (visit !== this.visit) return;
          attach(next, dn, next.get(dn)?.parent ?? null, branch);
          queue.push(...(next.get(dn)?.children ?? []).filter((child) => expanded.has(child)));
        } catch (error) {
          if (visit !== this.visit) return;
          this.treeError.set(safeMessage(error));
        }
      }
      this.root.set(rootDn);
      this.writable.set(result.writable);
      this.nodes.set(next);
      this.expanded.set(new Set([rootDn, ...[...expanded].filter((dn) => next.get(dn)?.children)]));
      this.details = new Map();
      if (!next.has(this.node() ?? '')) this.choose(rootDn);
      else this.people.reload();
      if (this.detailSid()) this.detail.reload();
    } catch (error) {
      if (visit === this.visit) this.treeError.set(safeMessage(error));
    } finally {
      if (visit === this.visit) this.refreshing.set(false);
    }
  }

  choose(dn: string) {
    if (dn === this.node()) return;
    this.node.set(dn);
    this.resetList();
    this.limitNote.set('');
    const state = this.nodes().get(dn);
    if (state?.node.hasChildren) this.expand(dn, true);
  }
  expand(dn: string, open: boolean) {
    this.expanded.update((set) => {
      const next = new Set(set);
      if (open) next.add(dn);
      else next.delete(dn);
      return next;
    });
    const state = this.nodes().get(dn);
    if (open && state && !state.children && !state.loading) void this.loadChildren(dn);
  }
  setSubtree(on: boolean) {
    this.subtree.set(on);
    this.anchor = null;
  }
  setFilter(filter: PeopleFilter) {
    this.filter.set(filter);
    this.anchor = null;
  }
  /** 節點內搜尋：畫面上立即篩選；節點超過上限時停止輸入一下再到目錄端找人。 */
  setQuery(value: string) {
    this.query.set(value);
    this.anchor = null;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.serverQuery.set(value), SEARCH_DELAY);
  }
  clearFilters() {
    this.query.set('');
    this.serverQuery.set('');
    this.filter.set('all');
    this.hideDisabled.set(false);
    this.anchor = null;
  }

  /** 勾選或取消一人；`range`（Shift）時把上一次點選到這個人之間的整段設成同一狀態。 */
  toggle(person: DirectoryPersonDto, checked: boolean, range = false) {
    const visible = this.visible();
    const from = range && this.anchor ? visible.findIndex((item) => item.sid === this.anchor) : -1;
    const to = visible.findIndex((item) => item.sid === person.sid);
    const people =
      from >= 0 && to >= 0 ? visible.slice(Math.min(from, to), Math.max(from, to) + 1) : [person];
    this.anchor = person.sid;
    this.apply(people, checked);
  }
  toggleAll(checked: boolean) {
    this.anchor = null;
    this.apply(this.visible(), checked);
  }
  remove(people: readonly DirectoryPersonDto[]) {
    this.apply(people, false);
  }
  clear() {
    this.selection.set(new Map());
    this.limitNote.set('');
  }
  /** 檢視一個人的 AD 屬性（點列、在匯入籃點人）並切到屬性面板；`null` 清除。 */
  inspect(person: DirectoryPersonDto | null, show = true) {
    clearTimeout(this.detailTimer);
    const sid = person?.sid ?? null;
    this.inspected.set(sid);
    if (sid && this.details.has(sid)) this.detailSid.set(sid);
    else {
      // 先放掉上一位（取消還在讀的請求），停留夠久才讀新的這位。
      this.detailSid.set(null);
      if (sid) this.detailTimer = setTimeout(() => this.detailSid.set(sid), DETAIL_DELAY);
    }
    if (sid && show) this.panel.set('attributes');
  }
  /** 已選的人換成清單的最新資料；變成無法匯入（衝突、已刪除）的人移出匯入籃並說明。 */
  private refreshSelection(people: readonly DirectoryPersonDto[]) {
    const current = this.selection();
    if (!people.some((person) => current.has(person.sid))) return;
    const next = new Map(current);
    let dropped = 0;
    for (const person of people) {
      if (!next.has(person.sid)) continue;
      if (selectable(person)) next.set(person.sid, person);
      else {
        next.delete(person.sid);
        dropped++;
      }
    }
    this.selection.set(next);
    if (dropped) this.limitNote.set(`${dropped} 人的狀態已變成無法匯入，已移出匯入籃。`);
  }

  private apply(people: readonly DirectoryPersonDto[], checked: boolean) {
    const { selection, skipped } = selectPeople(this.selection(), people, checked);
    this.selection.set(selection);
    this.limitNote.set(
      skipped ? `一次最多匯入 ${this.limit} 人，另有 ${skipped} 人沒有加入。` : '',
    );
  }
  private resetList() {
    this.filter.set('all');
    this.query.set('');
    this.serverQuery.set('');
    clearTimeout(this.searchTimer);
    this.anchor = null;
  }

  private async loadRoot(pick: boolean) {
    const visit = this.visit;
    this.treeError.set('');
    try {
      const result = await this.api.directoryNodes(undefined);
      if (visit !== this.visit) return;
      const dn = result.node.dn;
      this.root.set(dn);
      this.writable.set(result.writable);
      this.nodes.update((map) => attach(new Map(map), dn, null, result));
      this.expanded.update((set) => new Set([...set, dn]));
      if (pick || !this.nodes().has(this.node() ?? '')) {
        // 人員多半集中在一個容器（例如 CN=Users），預設打開人最多的那一層，少一次點選。
        const busiest = [...result.children].sort((a, b) => (b.people ?? 0) - (a.people ?? 0))[0];
        this.node.set(busiest?.people ? busiest.dn : dn);
      }
    } catch (error) {
      if (visit === this.visit) this.treeError.set(safeMessage(error));
    }
  }
  private async loadChildren(dn: string) {
    const visit = this.visit;
    this.patch(dn, { loading: true });
    this.treeError.set('');
    try {
      const result = await this.api.directoryNodes(dn);
      if (visit !== this.visit) return;
      this.nodes.update((map) => attach(new Map(map), dn, map.get(dn)?.parent ?? null, result));
    } catch (error) {
      if (visit !== this.visit) return;
      this.patch(dn, { loading: false });
      this.treeError.set(safeMessage(error));
      this.expanded.update((set) => {
        const next = new Set(set);
        next.delete(dn);
        return next;
      });
    }
  }
  private patch(dn: string, change: Partial<NodeState>) {
    this.nodes.update((map) => {
      const state = map.get(dn);
      if (!state) return map;
      const next = new Map(map);
      next.set(dn, { ...state, ...change });
      return next;
    });
  }
}
