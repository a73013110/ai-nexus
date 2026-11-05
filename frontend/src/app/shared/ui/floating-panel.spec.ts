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
    expect(labels).toEqual(['全螢幕', '縮小成動態島']);

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
