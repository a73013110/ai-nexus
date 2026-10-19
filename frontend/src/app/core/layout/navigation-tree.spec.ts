import { describe, expect, it } from 'vitest';
import type { NavigationNodeDto } from '@core/api/schema';
import {
  featureIcon,
  navigationEntries,
  navigationIcon,
  navigationNodes,
  navigationPages,
  navigationRuns,
  navigationSections,
} from './navigation-tree';

const page = (featureId: string, id = featureId): NavigationNodeDto => ({
  id,
  kind: 'page',
  name: featureId,
  icon: featureId,
  featureId,
  route: '/' + featureId,
  url: null,
  openInNewTab: false,
  children: [],
});
const link = (id: string, icon: string | null = null): NavigationNodeDto => ({
  id,
  kind: 'link',
  name: id,
  icon,
  featureId: null,
  route: null,
  url: 'https://example.com/' + id,
  openInNewTab: true,
  children: [],
});
const group = (id: string, name: string, children: NavigationNodeDto[]): NavigationNodeDto => ({
  id,
  kind: 'group',
  name,
  icon: null,
  featureId: null,
  route: null,
  url: null,
  openInNewTab: false,
  children,
});
// 伺服器回傳的樹：最上層的頁面與連結可以夾在分群之間，順序就是管理員排的順序。
const tree = [
  page('dashboard'),
  group('work', 'AI 工作台', [page('chat'), page('files')]),
  link('wiki', 'globe'),
  group('system', '系統管理', [
    page('audit'),
    group('system.diagnostics', '診斷', [page('logs'), link('runbook')]),
    page('chat', 'system.chat'),
  ]),
];
const ids = (list: readonly { id: string }[]) => list.map((item) => item.id);

describe('navigation tree', () => {
  it('flattens to sections with paths and depth, the top level first when it has entries', () => {
    const sections = navigationSections(tree, { topName: '最上層' });
    expect(
      sections.map((section) => [section.id, section.path, section.depth, ids(section.entries)]),
    ).toEqual([
      ['', '最上層', 0, ['dashboard', 'wiki']],
      ['work', 'AI 工作台', 0, ['chat', 'files']],
      ['system', '系統管理', 0, ['audit', 'system.chat']],
      ['system.diagnostics', '系統管理 › 診斷', 1, ['logs', 'runbook']],
    ]);
    expect(navigationSections([tree[1]]).map((section) => section.id)).toEqual(['work']);
    expect(navigationSections([], { keepTop: true })).toHaveLength(1);
  });

  it('splits one level into runs of entries and groups without reordering', () => {
    expect(navigationRuns(tree).map((run) => [run.kind, run.key])).toEqual([
      ['entries', 'entries:dashboard'],
      ['group', 'work'],
      ['entries', 'entries:wiki'],
      ['group', 'system'],
    ]);
    const system = navigationRuns(tree[3].children);
    expect(system.map((run) => (run.kind === 'entries' ? ids(run.nodes) : run.node.id))).toEqual([
      ['audit'],
      'system.diagnostics',
      ['system.chat'],
    ]);
  });

  it('bundles a self-navigating module’s pages into one entry at its first page, others stay', () => {
    const entries = navigationEntries([
      page('access-control.roles'),
      page('knowledge'),
      page('access-control.navigation'),
      page('knowledge.retrieval'),
      page('quality.retrieval'),
      link('wiki'),
    ]);
    expect(entries.map((entry) => [entry.node.id, entry.node.name, ids(entry.members)])).toEqual([
      ['module:access-control', '存取控制', ['access-control.roles', 'access-control.navigation']],
      ['knowledge', 'knowledge', ['knowledge']],
      ['knowledge.retrieval', 'knowledge.retrieval', ['knowledge.retrieval']],
      ['quality.retrieval', 'quality.retrieval', ['quality.retrieval']],
      ['wiki', 'wiki', ['wiki']],
    ]);
    expect(entries[0].node.route).toBe('/access-control.roles');
  });

  it('lists every node depth first and each page once, at its first place', () => {
    expect(ids(navigationNodes(tree))).toEqual([
      'dashboard',
      'work',
      'chat',
      'files',
      'wiki',
      'system',
      'audit',
      'system.diagnostics',
      'logs',
      'runbook',
      'system.chat',
    ]);
    expect(navigationPages(tree)).toEqual([
      { id: 'dashboard', name: 'dashboard', route: '/dashboard', icon: 'dashboard' },
      { id: 'chat', name: 'chat', route: '/chat', icon: 'chat' },
      { id: 'files', name: 'files', route: '/files', icon: 'files' },
      { id: 'audit', name: 'audit', route: '/audit', icon: 'audit' },
      { id: 'logs', name: 'logs', route: '/logs', icon: 'logs' },
    ]);
  });

  it('marks links with the external icon unless they name one the menu can draw', () => {
    expect(navigationIcon(link('wiki', 'globe'))).toBe('globe');
    expect(navigationIcon(link('wiki'))).toBe('external');
    expect(navigationIcon(link('wiki', 'no-such-icon'))).toBe('external');
    expect(navigationIcon(page('no-such-icon'))).toBe('document');
    expect(navigationIcon(group('g', 'G', []))).toBeNull();
  });

  it('falls back to one neutral icon for names the menu cannot draw', () => {
    expect(featureIcon('chat')).toBe('chat');
    expect(featureIcon('no-such-icon')).toBe('document');
    expect(featureIcon('')).toBe('document');
  });
});
