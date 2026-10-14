import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { StatGrid, StatTile } from './stat-tile';

@Component({
  selector: 'nx-stat-host',
  imports: [StatGrid, StatTile],
  template: `<nx-stat-grid label="平台用量摘要">
    <nx-stat-tile
      label="生成次數"
      [value]="value()"
      unit="次"
      hint="完成 3 · 失敗 1"
      [tone]="tone()"
    />
  </nx-stat-grid>`,
})
class Host {
  readonly value = signal<string | null>(null);
  readonly tone = signal<'neutral' | 'warning'>('neutral');
}

describe('StatTile', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('lists the figures and holds the value line while it loads', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const grid = element.querySelector('nx-stat-grid')!;
    const tile = element.querySelector('nx-stat-tile')!;
    expect(grid.getAttribute('role')).toBe('list');
    expect(grid.getAttribute('aria-label')).toBe('平台用量摘要');
    expect(tile.getAttribute('role')).toBe('listitem');
    expect(tile.getAttribute('aria-busy')).toBe('true');
    expect(tile.querySelector('.skeleton')).not.toBeNull();
    expect(tile.querySelector('.stat-value')).toBeNull();

    fixture.componentInstance.value.set('18,452');
    fixture.componentInstance.tone.set('warning');
    fixture.detectChanges();
    expect(tile.getAttribute('aria-busy')).toBe('false');
    expect(tile.querySelector('.skeleton')).toBeNull();
    expect(tile.querySelector('.stat-value')?.textContent?.replace(/\s+/g, '')).toBe('18,452次');
    expect(tile.querySelector('.stat-hint')?.textContent).toBe('完成 3 · 失敗 1');
    expect(tile.getAttribute('data-tone')).toBe('warning');
  });
});
