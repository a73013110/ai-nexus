import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IdleChrome, IdleChromePart } from './idle-chrome';

@Component({
  selector: 'nx-idle-chrome-host',
  imports: [IdleChrome, IdleChromePart],
  template: `<section nxIdleChrome #scope="nxIdleChrome">
    <header class="bar" [nxIdleChromePart]="immersive()">
      <button type="button" class="tool">放大</button>
      <button type="button" class="menu" [attr.aria-expanded]="expanded()">縮放</button>
    </header>
    <p class="content">內容 <a href="#x" class="link">連結</a></p>
  </section>`,
})
class Host {
  readonly immersive = signal(true);
  readonly expanded = signal(false);
}

/** 以一般事件帶上 IdleChrome 會讀的欄位（jsdom 的 PointerEvent 不帶座標）。 */
function pointer(type: string, init: { pointerType?: string; x?: number; y?: number } = {}) {
  return Object.assign(new Event(type, { bubbles: type !== 'pointerenter' }), {
    pointerType: init.pointerType ?? 'mouse',
    clientX: init.x ?? 0,
    clientY: init.y ?? 0,
  });
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  document.body.append(fixture.nativeElement);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const scope = root.querySelector('section')!;
  const bar = root.querySelector<HTMLElement>('.bar')!;
  const content = root.querySelector<HTMLElement>('.content')!;
  // 標題列在內容區頂端 0–40px，內容在其下方。
  bar.getBoundingClientRect = () => new DOMRect(0, 0, 800, 40);
  const state = () => {
    fixture.detectChanges();
    return bar.getAttribute('data-chrome');
  };
  const idle = (ms = 2000) => {
    fixture.detectChanges();
    vi.advanceTimersByTime(ms);
    return state();
  };
  return { fixture, host: fixture.componentInstance, root, scope, bar, content, state, idle };
}

describe('IdleChrome', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
    document.body.replaceChildren();
  });

  it('shows on open and fades after the idle delay', () => {
    const { state, idle } = setup();
    expect(state()).toBe('shown');
    expect(idle(1999)).toBe('shown');
    expect(idle(1)).toBe('hidden');
  });

  it('ignores reading movement and clicks over the content, but reveals near the chrome', () => {
    const { content, idle, state } = setup();
    expect(idle()).toBe('hidden');
    for (const y of [200, 220, 240, 400])
      content.dispatchEvent(pointer('pointermove', { x: 300, y }));
    content.dispatchEvent(pointer('pointerdown', { x: 300, y: 300 }));
    content.dispatchEvent(pointer('click', { x: 300, y: 300 }));
    expect(state()).toBe('hidden');
    content.dispatchEvent(pointer('pointermove', { x: 300, y: 60 }));
    expect(state()).toBe('shown');
    expect(idle()).toBe('hidden');
  });

  it('stays while the pointer rests on the chrome', () => {
    const { bar, idle } = setup();
    bar.dispatchEvent(pointer('pointerenter'));
    expect(idle(6000)).toBe('shown');
    bar.dispatchEvent(pointer('pointerleave'));
    expect(idle()).toBe('hidden');
  });

  it('toggles on a touch tap over the content, not on drags or links', () => {
    const { content, root, idle, state } = setup();
    expect(idle()).toBe('hidden');
    content.dispatchEvent(pointer('pointerdown', { pointerType: 'touch', x: 10, y: 300 }));
    content.dispatchEvent(pointer('click', { x: 10, y: 300 }));
    expect(state()).toBe('shown');
    content.dispatchEvent(pointer('pointerdown', { pointerType: 'touch', x: 10, y: 300 }));
    content.dispatchEvent(pointer('click', { x: 10, y: 300 }));
    expect(state()).toBe('hidden');
    content.dispatchEvent(pointer('pointerdown', { pointerType: 'touch', x: 10, y: 300 }));
    content.dispatchEvent(pointer('click', { x: 10, y: 360 }));
    const link = root.querySelector('.link')!;
    link.dispatchEvent(pointer('pointerdown', { pointerType: 'touch', x: 10, y: 300 }));
    link.dispatchEvent(pointer('click', { x: 10, y: 300 }));
    expect(state()).toBe('hidden');
    // 觸控的 pointerenter 不算停留，也不會被誤認成滑鼠移近。
    content.dispatchEvent(pointer('pointermove', { pointerType: 'touch', x: 10, y: 10 }));
    expect(state()).toBe('hidden');
  });

  it('reveals when focus enters the chrome and stays while it is there', () => {
    const { root, idle, state } = setup();
    expect(idle()).toBe('hidden');
    const tool = root.querySelector<HTMLButtonElement>('.tool')!;
    // 鍵盤操作後的焦點才是 :focus-visible；滑鼠點過的按鈕不會讓 chrome 一直留著。
    root
      .querySelector('.content')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
    tool.focus();
    expect(state()).toBe('shown');
    expect(idle(6000)).toBe('shown');
    tool.blur();
    expect(idle()).toBe('hidden');
  });

  it('stays while a control is expanded and hides on Escape otherwise', () => {
    const { host, scope, idle, state } = setup();
    host.expanded.set(true);
    expect(idle(6000)).toBe('shown');
    scope.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(state()).toBe('shown');
    host.expanded.set(false);
    state();
    scope.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(state()).toBe('hidden');
  });

  it('leaves inactive parts alone and reveals briefly when a part joins or the dialog opens', () => {
    const { host, scope, idle, state } = setup();
    host.immersive.set(false);
    expect(state()).toBeNull();
    host.immersive.set(true);
    expect(state()).toBe('shown');
    expect(idle()).toBe('hidden');
    scope.dispatchEvent(Object.assign(new Event('toggle'), { newState: 'open' }));
    expect(state()).toBe('shown');
  });
});
