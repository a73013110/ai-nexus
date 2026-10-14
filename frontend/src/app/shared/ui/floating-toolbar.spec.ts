import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FloatingToolbar } from './floating-toolbar';

@Component({
  selector: 'nx-floating-toolbar-host',
  imports: [FloatingToolbar],
  template: `<div class="region" #region>
    <nx-floating-toolbar label="檢視工具" [region]="region" [pinned]="pinned()">
      <button type="button" class="tool">放大</button>
      <button type="button" class="menu" [attr.aria-expanded]="expanded()">縮放</button>
    </nx-floating-toolbar>
    <p class="content" tabindex="-1">內容</p>
  </div>`,
})
class Host {
  readonly pinned = signal(false);
  readonly expanded = signal(false);
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  document.body.append(fixture.nativeElement);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const toolbar = root.querySelector('nx-floating-toolbar')!;
  const hidden = () => {
    fixture.detectChanges();
    return toolbar.classList.contains('is-hidden');
  };
  const idle = (ms = 2000) => {
    fixture.detectChanges();
    vi.advanceTimersByTime(ms);
    return hidden();
  };
  return { fixture, host: fixture.componentInstance, root, toolbar, hidden, idle };
}

describe('FloatingToolbar', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
    document.body.replaceChildren();
  });

  it('names its group, shows on open and fades after the idle delay', () => {
    const { toolbar, hidden, idle } = setup();
    expect(toolbar.querySelector('[role="group"]')?.getAttribute('aria-label')).toBe('檢視工具');
    expect(hidden()).toBe(false);
    expect(idle(1999)).toBe(false);
    expect(idle(1)).toBe(true);
  });

  it('comes back when the pointer moves or a touch lands in the region, and restarts the delay', () => {
    const { root, idle, hidden } = setup();
    expect(idle()).toBe(true);
    const content = root.querySelector('.content')!;
    content.dispatchEvent(new Event('pointermove', { bubbles: true }));
    expect(hidden()).toBe(false);
    expect(idle(1500)).toBe(false);
    content.dispatchEvent(new Event('pointermove', { bubbles: true }));
    expect(idle(1500)).toBe(false);
    expect(idle(500)).toBe(true);
    content.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(hidden()).toBe(false);
  });

  it('stays while focus is inside and fades once it leaves', () => {
    const { root, idle } = setup();
    root.querySelector<HTMLButtonElement>('.tool')!.focus();
    expect(idle(6000)).toBe(false);
    root.querySelector<HTMLElement>('.content')!.focus();
    expect(idle()).toBe(true);
  });

  it('reveals when keyboard focus enters the region', () => {
    const { root, idle, hidden } = setup();
    expect(idle()).toBe(true);
    root.querySelector<HTMLElement>('.content')!.focus();
    expect(hidden()).toBe(false);
  });

  it('stays while a control is expanded or the host pins it', () => {
    const { host, idle, hidden } = setup();
    host.expanded.set(true);
    expect(idle(6000)).toBe(false);
    host.expanded.set(false);
    expect(idle()).toBe(true);
    host.pinned.set(true);
    expect(hidden()).toBe(false);
    expect(idle(6000)).toBe(false);
    host.pinned.set(false);
    expect(idle()).toBe(true);
  });

  it('stays while the pointer rests on the bar', () => {
    const { toolbar, idle } = setup();
    const bar = toolbar.querySelector('.floating-toolbar')!;
    bar.dispatchEvent(new Event('pointerenter'));
    expect(idle(6000)).toBe(false);
    bar.dispatchEvent(new Event('pointerleave'));
    expect(idle()).toBe(true);
  });
});
