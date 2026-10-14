import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import type { DashboardSectionDto } from '@core/api/schema';
import {
  mergeTiles,
  moveTile,
  parseTiles,
  updateTile,
  type PlacedTile,
  type TileState,
} from './dashboard-layout';
import { DashboardTile } from './dashboard-tile';
import { dashboardWidgets, TILE_SIZES, type TileSize } from '../widgets/dashboard-widgets';

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

  it('keeps one arrangement for everyone; the platform scope only adds the user table', () => {
    const sections = [section('chat'), section('knowledge')];
    const personal = dashboardWidgets({ scope: 'personal', sections }).map((x) => x.id);
    const platform = dashboardWidgets({ scope: 'platform', sections }).map((x) => x.id);
    expect(platform.filter((x) => x !== 'users')).toEqual(personal);
    expect(platform.slice(0, 4)).toEqual(['overview', 'spend', 'activity', 'users']);
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
    expect(ids.at(-1)).toBe('tokens');
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

  it('ignores retired administrator tiles in arrangements saved by earlier versions', () => {
    const saved = parseTiles({
      v: 1,
      tiles: [
        { id: 'section:monitoring', size: 'l' },
        { id: 'section:platform', size: 'l' },
        { id: 'services', size: 'l' },
        { id: 'spend', size: 's' },
      ],
    });
    expect(saved).toEqual([{ id: 'spend', size: 's', tall: false, hidden: false }]);
    const ids = mergeTiles(widgets, saved).map((x) => x.id);
    expect(ids).not.toContain('section:monitoring');
    expect(ids).not.toContain('services');
  });

  it('resizes between every pair of sizes, through a save and reload', () => {
    const sizes = TILE_SIZES.map((x) => x.value);
    for (const from of sizes)
      for (const to of sizes) {
        const start = updateTile(mergeTiles(widgets, null), 'spend', { size: from });
        const saved = parseTiles({ v: 1, tiles: updateTile(start, 'spend', { size: to }) });
        expect(mergeTiles(widgets, saved).find((x) => x.id === 'spend')?.size).toBe(to);
      }
  });

  it('moves a tile to the place of another in either direction', () => {
    const tiles = ['a', 'b', 'c', 'd'].map((id) => tile(id));
    expect(moveTile(tiles, 'a', 'c').map((x) => x.id)).toEqual(['b', 'c', 'a', 'd']);
    expect(moveTile(tiles, 'd', 'b').map((x) => x.id)).toEqual(['a', 'd', 'b', 'c']);
    expect(moveTile(tiles, 'a', 'missing').map((x) => x.id)).toEqual(['a', 'b', 'c', 'd']);
  });
});

@Component({ template: '' })
class Empty {}

describe('dashboard tile', () => {
  // Regression: narrow tiles used to hide the size choices, so a tile made small could not grow.
  it('offers every size while editing, whatever the current size', () => {
    for (const size of TILE_SIZES.map((x) => x.value)) {
      const fixture = TestBed.createComponent(DashboardTile);
      const tile: PlacedTile = {
        id: 'spend',
        size,
        tall: false,
        hidden: false,
        widget: {
          id: 'spend',
          title: '費用趨勢',
          description: '',
          icon: 'money',
          size: 'w',
          component: Empty,
        },
      };
      fixture.componentRef.setInput('tile', tile);
      fixture.componentRef.setInput('editing', true);
      const chosen: TileSize[] = [];
      fixture.componentInstance.sizeChange.subscribe((x) => chosen.push(x));
      fixture.detectChanges();
      const buttons = [
        ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
          '.tile-sizes button',
        ),
      ];
      expect(buttons.map((x) => x.textContent?.trim())).toEqual(TILE_SIZES.map((x) => x.label));
      buttons.forEach((x) => x.click());
      expect(chosen).toEqual(TILE_SIZES.map((x) => x.value));
    }
  });
});
