import { Component, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FloatingPanel } from './floating-panel';

@Component({
  imports: [FloatingPanel],
  template: `<nx-floating-panel title="示範面板" islandLabel="示範面板，展開" storageKey="spec">
    <textarea autofocus aria-label="內容"></textarea>
    <span floating-island>示範面板</span>
  </nx-floating-panel>`,
})
class Host {
  readonly panel = viewChild.required(FloatingPanel);
}

@Component({
  imports: [FloatingPanel],
  template: `<nx-floating-panel
    title="助理"
    islandLabel="助理，展開"
    storageKey="assistant"
    [docked]="true"
  >
    <span floating-island>助理</span>
  </nx-floating-panel>`,
})
class DockedHost {
  readonly panel = viewChild.required(FloatingPanel);
}

const pointer = (type: string, x: number, y: number) =>
  new MouseEvent(type, { bubbles: true, button: 0, clientX: x, clientY: y });

describe('FloatingPanel', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    vi.spyOn(window, 'innerWidth', 'get').mockReturnValue(1024);
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(768);
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  async function open() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const panel = fixture.componentInstance.panel();
    panel.open();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    return { fixture, panel, host, layer: host.querySelector<HTMLElement>('.ui-floating')! };
  }

  // jsdom 沒有 popover（裡面的元素不可聚焦），焦點交接在瀏覽器驗證。
  it('opens at the top right, minimizes into the island and grows back without a close button', async () => {
    const { fixture, panel, host, layer } = await open();
    expect(panel.mode()).toBe('panel');
    expect(layer.style.getPropertyValue('--floating-x')).toBe('608px');
    const labels = [...host.querySelectorAll('.ui-floating-actions button')].map((button) =>
      button.getAttribute('aria-label'),
    );
    expect(labels).toEqual(['縮小成動態島', '全螢幕']);

    host.querySelector<HTMLButtonElement>('[aria-label="縮小成動態島"]')!.click();
    await fixture.whenStable();
    const island = host.querySelector<HTMLButtonElement>('.ui-floating-island-open')!;
    expect(panel.mode()).toBe('island');
    expect(layer.dataset['mode']).toBe('island');
    expect(island.getAttribute('aria-label')).toBe('示範面板，展開');
    expect(host.querySelector<HTMLElement>('.ui-floating-panel')!.inert).toBe(true);

    island.click();
    await fixture.whenStable();
    expect(panel.mode()).toBe('panel');
    expect(layer.style.getPropertyValue('--floating-x')).toBe('608px');
  });

  async function docked() {
    const fixture = TestBed.createComponent(DockedHost);
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      panel: fixture.componentInstance.panel(),
      layer: host.querySelector<HTMLElement>('.ui-floating')!,
      island: host.querySelector<HTMLButtonElement>('.ui-floating-island-open')!,
    };
  }

  it('docks the island at the top center and grows the panel down from it', async () => {
    const { fixture, panel, layer, island } = await docked();
    expect(panel.mode()).toBe('island');
    expect(layer.dataset['side']).toBe('center');
    expect(layer.dataset['edge']).toBe('top');
    expect(layer.style.getPropertyValue('--floating-y')).toBe('16px');

    // 指到島上時讓呼叫端露出說明。
    island.parentElement!.dispatchEvent(new MouseEvent('pointerenter'));
    expect(panel.peek()).toBe(true);
    island.parentElement!.dispatchEvent(new MouseEvent('pointerleave'));
    expect(panel.peek()).toBe(false);

    island.click();
    await fixture.whenStable();
    expect(panel.mode()).toBe('panel');
    expect(layer.style.getPropertyValue('--floating-x')).toBe('312px');
    expect(layer.style.getPropertyValue('--floating-y')).toBe('16px');

    panel.minimize();
    await fixture.whenStable();
    expect(layer.style.getPropertyValue('--floating-y')).toBe('16px');
  });

  it('lets the docked island move, remembers it and grows the panel from its corner', async () => {
    const { fixture, panel, layer, island } = await docked();
    island.parentElement!.dispatchEvent(new MouseEvent('pointerenter'));
    island.dispatchEvent(pointer('pointerdown', 512, 30));
    island.dispatchEvent(pointer('pointermove', 900, 700));
    await fixture.whenStable();
    expect(layer.dataset['gesture']).toBe('move');
    // 拖的是按下時的樣子：指標暫時落在島外也不收起說明；放開後依指標實際位置校正。
    island.parentElement!.dispatchEvent(new MouseEvent('pointerleave'));
    expect(panel.peek()).toBe(true);
    island.dispatchEvent(pointer('pointerup', 900, 700));
    await fixture.whenStable();
    expect(panel.peek()).toBe(false);
    // 放開時靠近右下角，吸附到邊距。
    expect(layer.style.getPropertyValue('--floating-x')).toBe('848px');
    expect(layer.style.getPropertyValue('--floating-y')).toBe('712px');
    expect(JSON.parse(localStorage.getItem('ai-nexus.floating.assistant')!).island).toEqual({
      side: 'end',
      inline: 16,
      edge: 'bottom',
      block: 16,
    });

    panel.expand();
    await fixture.whenStable();
    expect(layer.dataset['edge']).toBe('bottom');
    expect(layer.style.getPropertyValue('--floating-x')).toBe('608px');
    expect(layer.style.getPropertyValue('--floating-y')).toBe('112px');
    // 面板拖到別處再縮小：島回到自己的位置，不跟著面板。
    const header = layer.querySelector('.ui-floating-header')!;
    header.dispatchEvent(pointer('pointerdown', 700, 130));
    header.dispatchEvent(pointer('pointermove', 400, 300));
    header.dispatchEvent(pointer('pointerup', 400, 300));
    panel.minimize();
    await fixture.whenStable();
    expect(layer.style.getPropertyValue('--floating-x')).toBe('848px');

    fixture.destroy();
    const again = await docked();
    expect(again.layer.style.getPropertyValue('--floating-y')).toBe('712px');
  });

  it('steps back on Escape: full screen to panel, then panel to island', async () => {
    const { fixture, panel, host } = await open();
    host.querySelector<HTMLButtonElement>('[aria-label="全螢幕"]')!.click();
    await fixture.whenStable();
    expect(panel.mode()).toBe('full');
    const escape = () =>
      host
        .querySelector('textarea')!
        .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    escape();
    expect(panel.mode()).toBe('panel');
    escape();
    expect(panel.mode()).toBe('island');
  });

  it('drags by the header, snaps near an edge and remembers the place', async () => {
    const { fixture, host, layer } = await open();
    const header = host.querySelector('.ui-floating-header')!;
    header.dispatchEvent(pointer('pointerdown', 700, 30));
    header.dispatchEvent(pointer('pointermove', 702, 31));
    expect(layer.hasAttribute('data-gesture')).toBe(false);
    header.dispatchEvent(pointer('pointermove', 300, 230));
    await fixture.whenStable();
    expect(layer.hasAttribute('data-gesture')).toBe(true);
    header.dispatchEvent(pointer('pointerup', 300, 230));
    await fixture.whenStable();
    expect(layer.hasAttribute('data-gesture')).toBe(false);
    expect(layer.style.getPropertyValue('--floating-x')).toBe('208px');
    // 放開時離下緣不到吸附距離，貼齊到下緣的間距。
    expect(layer.style.getPropertyValue('--floating-y')).toBe('112px');
    expect(JSON.parse(localStorage.getItem('ai-nexus.floating.spec')!).placement).toMatchObject({
      side: 'start',
      inline: 208,
      edge: 'bottom',
      block: 16,
    });

    fixture.destroy();
    const again = await open();
    expect(again.layer.style.getPropertyValue('--floating-x')).toBe('208px');
  });

  it('resizes from an edge but not in full screen', async () => {
    const { fixture, panel, host, layer } = await open();
    const edge = host.querySelector('.ui-floating-edge[data-edge="start"]')!;
    edge.dispatchEvent(pointer('pointerdown', 608, 300));
    edge.dispatchEvent(pointer('pointermove', 508, 300));
    await fixture.whenStable();
    // 拖到一半內容就跟著變大，不等放開。
    expect(layer.style.getPropertyValue('--panel-width')).toBe('500px');
    edge.dispatchEvent(pointer('pointerup', 508, 300));
    await fixture.whenStable();
    expect(layer.style.getPropertyValue('--floating-width')).toBe('500px');
    expect(layer.style.getPropertyValue('--floating-x')).toBe('508px');

    panel.toggleFull();
    await fixture.whenStable();
    expect(host.querySelector('.ui-floating-edge')).toBeNull();
    expect(layer.style.getPropertyValue('--floating-width')).toBe('1024px');
  });
});
