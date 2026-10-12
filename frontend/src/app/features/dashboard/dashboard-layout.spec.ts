import { describe, expect, it } from 'vitest';
import type { DashboardSectionDto } from '../../core/api/schema';
import { mergeTiles, moveTile, parseTiles, type TileState } from './dashboard-layout';
import { dashboardWidgets } from './dashboard-widgets';

function section(id: string): DashboardSectionDto {
  return {
    id,
    feature: id,
    title: id,
    route: '/' + id,
    state: 'idle',
    metrics: [],
    series: [],
    shares: [],
    items: [],
    facts: [],
  };
}
const tile = (id: string, extra: Partial<TileState> = {}): TileState => ({
  id,
  size: 'm',
  tall: false,
  hidden: false,
  ...extra,
});

describe('dashboard widgets', () => {
  it('lists known tiles in the default order and appends sections of new features', () => {
    const ids = dashboardWidgets({
      scope: 'personal',
      sections: [section('future-feature'), section('knowledge'), section('chat')],
    }).map((x) => x.id);
    expect(ids.slice(0, 5)).toEqual([
      'overview',
      'spend',
      'activity',
      'section:chat',
      'section:knowledge',
    ]);
    expect(ids.at(-1)).toBe('section:future-feature');
    expect(ids).not.toContain('users');
  });

  it('adds the user table and leads with platform sections for administrators', () => {
    const ids = dashboardWidgets({
      scope: 'platform',
      sections: [section('chat'), section('platform')],
    }).map((x) => x.id);
    expect(ids.slice(0, 3)).toEqual(['overview', 'section:platform', 'users']);
  });
});

describe('dashboard layout', () => {
  const widgets = dashboardWidgets({ scope: 'personal', sections: [section('chat')] });

  it('uses the defaults until the viewer arranges tiles', () => {
    expect(mergeTiles(widgets, null).map((x) => x.id)).toEqual(widgets.map((x) => x.id));
  });

  it('keeps the saved order and adds widgets never arranged at the end', () => {
    const saved = [tile('section:chat'), tile('overview', { size: 'l' }), tile('gone-feature')];
    const merged = mergeTiles(widgets, saved);
    const ids = merged.map((x) => x.id);
    expect(ids.slice(0, 4)).toEqual(['section:chat', 'overview', 'gone-feature', 'spend']);
    expect(ids.at(-1)).toBe('services');
    expect(merged.find((x) => x.id === 'overview')?.size).toBe('l');
  });

  it('reads saved arrangements defensively', () => {
    expect(parseTiles(null)).toBeNull();
    expect(parseTiles({ v: 1, tiles: 'x' })).toBeNull();
    expect(
      parseTiles({
        v: 1,
        tiles: [{ id: 'overview', size: 'huge' }, { id: 'spend', size: 'w', hidden: 'yes' }, 3],
      }),
    ).toEqual([{ id: 'spend', size: 'w', tall: false, hidden: false }]);
  });

  it('moves a tile to the place of another in either direction', () => {
    const tiles = ['a', 'b', 'c', 'd'].map((id) => tile(id));
    expect(moveTile(tiles, 'a', 'c').map((x) => x.id)).toEqual(['b', 'c', 'a', 'd']);
    expect(moveTile(tiles, 'd', 'b').map((x) => x.id)).toEqual(['a', 'd', 'b', 'c']);
    expect(moveTile(tiles, 'a', 'missing').map((x) => x.id)).toEqual(['a', 'b', 'c', 'd']);
  });
});
