import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { TreeView, type TreeItem } from './tree-view';

const items: TreeItem[] = [
  {
    id: 'root',
    label: 'fixture.internal',
    icon: 'globe',
    count: 9,
    badge: 2,
    badgeLabel: '已選 2 人',
    hasChildren: true,
    children: [
      {
        id: 'rd',
        label: '研發處',
        count: 3,
        hasChildren: true,
        children: [{ id: 'platform', label: '平台部', count: 2, hasChildren: false }],
      },
      { id: 'empty', label: '空部門', count: 0, muted: true, hasChildren: false },
      { id: 'lazy', label: '業務處', count: null, hasChildren: true, loading: true },
    ],
  },
];

@Component({
  selector: 'nx-tree-host',
  imports: [TreeView],
  template: `<nx-tree-view
    label="組織單位"
    [items]="items"
    [selected]="selected()"
    [expanded]="expanded()"
    (nodeSelect)="selected.set($event); picks.push($event)"
    (nodeToggle)="toggles.push($event)"
  />`,
})
class Host {
  readonly items = items;
  readonly selected = signal<string | null>('rd');
  readonly expanded = signal<ReadonlySet<string>>(new Set(['root']));
  readonly picks: string[] = [];
  readonly toggles: { id: string; open: boolean }[] = [];
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const tree = element.querySelector<HTMLElement>('[role="tree"]')!;
  const node = (id: string) => element.querySelector<HTMLElement>(`[data-id="${id}"]`);
  const press = (key: string) => {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
    document.activeElement!.dispatchEvent(event);
    fixture.detectChanges();
    return event;
  };
  return { fixture, host: fixture.componentInstance, element, tree, node, press };
}

describe('TreeView', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('renders the open branches as an ARIA tree with one tab stop on the selected node', () => {
    const { tree, node, element } = setup();
    expect(tree.getAttribute('aria-label')).toBe('組織單位');
    // 研發處沒展開，平台部不在畫面上。
    expect(
      [...element.querySelectorAll('[role="treeitem"]')].map((x) => x.getAttribute('data-id')),
    ).toEqual(['root', 'rd', 'empty', 'lazy']);
    expect(node('root')!.getAttribute('aria-level')).toBe('1');
    expect(node('root')!.getAttribute('aria-expanded')).toBe('true');
    expect(node('rd')!.getAttribute('aria-level')).toBe('2');
    expect(node('rd')!.getAttribute('aria-expanded')).toBe('false');
    expect(node('empty')!.hasAttribute('aria-expanded')).toBe(false);
    expect(node('rd')!.getAttribute('aria-selected')).toBe('true');
    expect(node('root')!.getAttribute('aria-selected')).toBe('false');
    expect(node('lazy')!.getAttribute('aria-busy')).toBe('true');
    expect(node('rd')!.getAttribute('tabindex')).toBe('0');
    expect(
      [...element.querySelectorAll('[role="treeitem"][tabindex="0"]')].map((x) =>
        x.getAttribute('data-id'),
      ),
    ).toEqual(['rd']);
  });

  it('shows counts, the picked badge with its name, and dims empty nodes', () => {
    const { node } = setup();
    expect(node('root')!.querySelector('.ui-tree-count')?.textContent).toBe('9');
    const badge = node('root')!.querySelector('.ui-tree-badge')!;
    expect(badge.textContent).toBe('2');
    expect(badge.getAttribute('aria-label')).toBe('已選 2 人');
    // 0 是有意義的人數，仍要顯示；badge 為 0 則不顯示。
    expect(node('empty')!.querySelector('.ui-tree-count')?.textContent).toBe('0');
    expect(node('empty')!.querySelector('.ui-tree-badge')).toBeNull();
    expect(node('empty')!.classList).toContain('ui-tree-muted');
    // 讀取中顯示轉圈而不是人數；人數不明（null）也不顯示。
    expect(node('lazy')!.querySelector('.ui-tree-count')).toBeNull();
    expect(node('lazy')!.querySelector('.ui-tree-spinner')).not.toBeNull();
  });

  it('asks to open or close a branch from the twisty without selecting it', () => {
    const { host, node } = setup();
    node('rd')!.querySelector<HTMLElement>('.ui-tree-twisty')!.click();
    node('root')!.querySelector<HTMLElement>('.ui-tree-twisty')!.click();
    node('empty')!.querySelector<HTMLElement>('.ui-tree-twisty')!.click();
    expect(host.toggles).toEqual([
      { id: 'rd', open: true },
      { id: 'root', open: false },
    ]);
    expect(host.picks).toEqual([]);

    node('empty')!.click();
    expect(host.picks).toEqual(['empty']);
  });

  it('moves focus with the arrow keys, Home and End, and selects with Enter', () => {
    const { fixture, host, node, press } = setup();
    node('rd')!.focus();
    press('ArrowDown');
    expect(document.activeElement).toBe(node('empty'));
    expect(node('empty')!.getAttribute('tabindex')).toBe('0');
    expect(node('rd')!.getAttribute('tabindex')).toBe('-1');
    press('End');
    expect(document.activeElement).toBe(node('lazy'));
    press('ArrowDown');
    expect(document.activeElement).toBe(node('lazy'));
    press('Home');
    expect(document.activeElement).toBe(node('root'));
    press('ArrowUp');
    expect(document.activeElement).toBe(node('root'));

    // 左鍵：在子節點上回到上層。
    node('empty')!.focus();
    press('ArrowLeft');
    expect(document.activeElement).toBe(node('root'));

    // 右鍵：收合的節點要求展開；展開後再按一次進到第一個子節點。
    node('rd')!.focus();
    press('ArrowRight');
    expect(host.toggles).toEqual([{ id: 'rd', open: true }]);
    host.expanded.set(new Set(['root', 'rd']));
    fixture.detectChanges();
    press('ArrowRight');
    expect(document.activeElement).toBe(node('platform'));
    expect(node('platform')!.getAttribute('aria-level')).toBe('3');

    // 左鍵：在展開的節點上要求收合。
    node('rd')!.focus();
    press('ArrowLeft');
    expect(host.toggles.at(-1)).toEqual({ id: 'rd', open: false });

    const enter = press('Enter');
    expect(enter.defaultPrevented).toBe(true);
    expect(host.picks).toEqual(['rd']);
    node('platform')!.focus();
    press(' ');
    expect(host.picks).toEqual(['rd', 'platform']);
    expect(node('platform')!.getAttribute('aria-selected')).toBe('true');

    // 其他按鍵（例如 Tab）不攔，讓焦點照常離開樹。
    expect(press('Tab').defaultPrevented).toBe(false);
  });
});
