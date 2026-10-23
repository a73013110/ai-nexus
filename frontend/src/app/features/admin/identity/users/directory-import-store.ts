import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { DirectoryNodeDto, DirectoryPersonDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import type { TreeItem } from '@shared/ui/tree-view';
import { IdentityApi } from '../identity-api';
import {
  DIRECTORY_BATCH_LIMIT,
  type DirectoryGroup,
  type DirectorySelection,
  groupOf,
  selectPeople,
  selectionState,
  withinDn,
} from './directory-outcomes';

/** 樹上一個已讀到的節點；`children` 是下一層的 DN，undefined 表示還沒展開過。 */
interface NodeState {
  node: DirectoryNodeDto;
  parent: string | null;
  children?: readonly string[];
  loading: boolean;
}

export type PeopleFilter = DirectoryGroup | 'all';
export type SidePanel = 'basket' | 'attributes';

const collator = new Intl.Collator('zh-Hant-TW');
const icons: Record<string, string> = { root: 'globe', ou: 'folder', container: 'container' };

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
  /** 服務帳號能否寫回 AD；目前一律唯讀，介面只預留位置。 */
  readonly writable = signal(false);

  readonly subtree = signal(false);
  readonly filter = signal<PeopleFilter>('all');
  readonly query = signal('');
  readonly hideDisabled = signal(false);
  readonly selection = signal<DirectorySelection>(new Map());
  readonly limitNote = signal('');
  /** Shift 連續選取的起點：上一次點選的人。換節點或篩選時清掉，避免跨清單選到看不到的人。 */
  private anchor: string | null = null;
  readonly inspected = signal<string | null>(null);
  readonly panel = signal<SidePanel>('basket');

  constructor() {
    // 選取當下的資料換成清單最新的值（換節點回來後狀態可能變了，例如剛匯入過），匯入籃的分組才會跟著更新。
    effect(() => {
      const people = this.all();
      untracked(() => this.refreshSelection(people));
    });
  }

  readonly people = apiResource({
    feature: 'identity.users.import',
    params: () => {
      const node = this.node();
      return this.opened() && node ? { node, subtree: this.subtree() } : undefined;
    },
    loader: (params, signal) => this.api.directoryPeople(params.node, params.subtree, signal),
  });

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

  /** 依中文排序（伺服器按 Unicode 排，筆畫順序對管理員比較好找）；Shift 連續選取也依這個順序。 */
  readonly all = computed(() =>
    [...(this.people.value()?.people ?? [])].sort(
      (a, b) =>
        collator.compare(a.displayName, b.displayName) || collator.compare(a.account, b.account),
    ),
  );
  readonly more = computed(() => this.people.value()?.more ?? false);
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
  /** 右側「AD 屬性」的人：優先用目前清單的最新資料，換節點後退回選取當下的資料。 */
  readonly inspectedPerson = computed(() => {
    const sid = this.inspected();
    if (!sid) return null;
    return this.all().find((person) => person.sid === sid) ?? this.selection().get(sid) ?? null;
  });

  open() {
    this.visit++;
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
    this.inspected.set(null);
    this.panel.set('basket');
    this.opened.set(true);
    void this.loadRoot(true);
  }
  close() {
    this.visit++;
    this.opened.set(false);
  }

  /** 重讀整棵樹：保留展開與目前節點，消失的節點就收合；人員清單一起重讀。 */
  async refresh() {
    this.visit++;
    const expanded = [...this.expanded()];
    this.nodes.set(new Map());
    await this.loadRoot(false);
    for (const dn of expanded)
      if (dn !== this.root() && this.nodes().has(dn)) await this.loadChildren(dn);
    this.people.reload();
  }

  choose(dn: string) {
    if (dn === this.node()) return;
    this.node.set(dn);
    this.resetList();
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
    if (open && !this.nodes().get(dn)?.children) void this.loadChildren(dn);
  }
  setSubtree(on: boolean) {
    this.subtree.set(on);
    this.anchor = null;
  }
  setFilter(filter: PeopleFilter) {
    this.filter.set(filter);
    this.anchor = null;
  }
  clearFilters() {
    this.query.set('');
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
  inspect(person: DirectoryPersonDto) {
    this.inspected.set(person.sid);
    this.panel.set('attributes');
  }
  private refreshSelection(people: readonly DirectoryPersonDto[]) {
    const current = this.selection();
    if (!people.some((person) => current.has(person.sid))) return;
    const next = new Map(current);
    for (const person of people) if (next.has(person.sid)) next.set(person.sid, person);
    this.selection.set(next);
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
      this.attach(dn, null, result.node, result.children);
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
    try {
      const result = await this.api.directoryNodes(dn);
      if (visit !== this.visit) return;
      this.attach(dn, this.nodes().get(dn)?.parent ?? null, result.node, result.children);
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
  private attach(
    dn: string,
    parent: string | null,
    node: DirectoryNodeDto,
    children: readonly DirectoryNodeDto[],
  ) {
    const sorted = [...children].sort((a, b) => collator.compare(a.name, b.name));
    this.nodes.update((map) => {
      const next = new Map(map);
      next.set(dn, { node, parent, children: sorted.map((child) => child.dn), loading: false });
      for (const child of sorted)
        next.set(child.dn, { ...next.get(child.dn), node: child, parent: dn, loading: false });
      return next;
    });
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
