import { Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { Island } from './island';
import { IslandService, type IslandItem } from './island-service';

const item = (id: string, priority: number, extra: Partial<IslandItem> = {}): IslandItem => ({
  id,
  priority,
  tone: 'info',
  icon: 'info',
  label: id,
  ...extra,
});

@Component({ selector: 'nx-island-host', imports: [Island], template: '<nx-island />' })
class Host {
  readonly items = signal<IslandItem[]>([]);
  constructor() {
    inject(IslandService).register(this.items);
  }
}

describe('Island', () => {
  afterEach(() => TestBed.resetTestingModule());
  const setup = () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const island = fixture.debugElement.children[0].componentInstance as Island;
    return { fixture, host: fixture.componentInstance, island };
  };

  it('orders notices by priority and drops them with their source', () => {
    const { fixture, host } = setup();
    host.items.set([item('low', 1), item('high', 9)]);
    expect(
      TestBed.inject(IslandService)
        .items()
        .map((item) => item.id),
    ).toEqual(['high', 'low']);
    fixture.destroy();
    expect(TestBed.inject(IslandService).items()).toEqual([]);
  });

  it('is a pill for one notice and splits for more', () => {
    const { host, island } = setup();
    expect(island.mode()).toBe('hidden');
    host.items.set([item('a', 1)]);
    expect(island.mode()).toBe('compact');
    host.items.set([item('a', 1), item('b', 2)]);
    expect(island.mode()).toBe('split');
    island.open();
    expect(island.mode()).toBe('expanded');
    host.items.set([]);
    expect(island.mode()).toBe('hidden');
    host.items.set([item('a', 1)]);
    expect(island.mode()).toBe('compact');
  });

  it('keeps a lone label-only notice as a status that cannot open', () => {
    const { fixture, host, island } = setup();
    host.items.set([item('live', 5, { urgent: true, message: 'live' })]);
    fixture.detectChanges();
    expect(island.expandable()).toBe(false);
    expect(island.mode()).toBe('compact');
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('button.island-pill')).toBeNull();
    expect(element.querySelector('[role="status"]')?.textContent).toBe('live');
    island.open();
    expect(island.mode()).toBe('compact');
    // 多出一則就能展開；那則離開後回到不能展開，之前的展開也不留著。
    host.items.set([item('live', 5), item('next', 1)]);
    island.open();
    expect(island.mode()).toBe('expanded');
    host.items.set([item('live', 5)]);
    expect(island.mode()).toBe('compact');
    host.items.set([item('live', 5), item('next', 1)]);
    expect(island.mode()).toBe('split');
  });

  it('opens a lone notice that has more to show', () => {
    const { host, island } = setup();
    for (const extra of [
      { message: '完整句子' },
      { action: { label: '重試', run: () => undefined } },
      { link: { label: '前往', commands: ['/'] } },
    ]) {
      host.items.set([item('one', 1, extra)]);
      expect(island.expandable()).toBe(true);
    }
  });

  it('expands urgent notices until collapsed, and again when they return', () => {
    const { fixture, host, island } = setup();
    host.items.set([item('warn', 5, { urgent: true, message: '請縮短內容' })]);
    expect(island.mode()).toBe('expanded');
    island.collapse();
    expect(island.mode()).toBe('compact');
    host.items.set([item('warn', 5, { urgent: true }), item('next', 6, { urgent: true })]);
    expect(island.mode()).toBe('expanded');
    island.collapse();
    host.items.set([]);
    fixture.detectChanges();
    host.items.set([item('warn', 5, { urgent: true, message: '請縮短內容' })]);
    expect(island.mode()).toBe('expanded');
  });
});
