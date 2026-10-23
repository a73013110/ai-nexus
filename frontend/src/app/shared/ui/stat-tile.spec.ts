import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { StatGrid, StatTile } from './stat-tile';

@Component({
  selector: 'nx-stat-host',
  imports: [StatGrid, StatTile],
  template: `<nx-stat-grid label="平台用量摘要" [busy]="busy()">
    <nx-stat-tile
      label="生成次數"
      [value]="value()"
      [loading]="loading()"
      unit="次"
      hint="完成 3 · 失敗 1"
      [tone]="tone()"
    />
  </nx-stat-grid>`,
})
class Host {
  readonly value = signal<string | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly tone = signal<'neutral' | 'warning'>('neutral');
}

describe('StatTile', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      host: fixture.componentInstance,
      grid: element.querySelector('nx-stat-grid')!,
      tile: element.querySelector('nx-stat-tile')!,
    };
  }

  it('lists the figures and holds the value line while it loads', () => {
    const { fixture, host, grid, tile } = setup();
    expect(grid.getAttribute('role')).toBe('list');
    expect(grid.getAttribute('aria-label')).toBe('平台用量摘要');
    expect(tile.getAttribute('role')).toBe('listitem');
    expect(tile.getAttribute('aria-busy')).toBe('true');
    expect(tile.querySelector('.skeleton')).not.toBeNull();
    expect(tile.querySelector('.stat-value')).toBeNull();

    host.value.set('18,452');
    host.loading.set(false);
    host.tone.set('warning');
    fixture.detectChanges();
    expect(tile.hasAttribute('aria-busy')).toBe(false);
    expect(tile.querySelector('.skeleton')).toBeNull();
    expect(tile.querySelector('.stat-value')?.textContent?.replace(/\s+/g, '')).toBe('18,452次');
    expect(tile.querySelector('.stat-hint')?.textContent).toBe('完成 3 · 失敗 1');
    expect(tile.getAttribute('data-tone')).toBe('warning');
  });

  it('shows an unknown value as a dash instead of a loading skeleton', () => {
    const { fixture, host, tile } = setup();
    host.loading.set(false);
    fixture.detectChanges();
    expect(tile.querySelector('.skeleton')).toBeNull();
    expect(tile.hasAttribute('aria-busy')).toBe(false);
    expect(tile.querySelector('.stat-value')?.textContent?.trim()).toBe('—');
  });

  it('dims the grid while figures already shown are refreshed', () => {
    const { fixture, host, grid } = setup();
    expect(grid.hasAttribute('aria-busy')).toBe(false);
    host.value.set('18,452');
    host.loading.set(false);
    host.busy.set(true);
    fixture.detectChanges();
    expect(grid.getAttribute('aria-busy')).toBe('true');
    expect(grid.classList).toContain('is-refreshing');
    host.busy.set(false);
    fixture.detectChanges();
    expect(grid.hasAttribute('aria-busy')).toBe(false);
    expect(grid.classList).not.toContain('is-refreshing');
  });
});
