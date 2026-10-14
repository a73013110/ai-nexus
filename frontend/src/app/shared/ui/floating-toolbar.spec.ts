import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FloatingToolbar } from './floating-toolbar';
import { IdleChrome } from './idle-chrome';

@Component({
  selector: 'nx-floating-toolbar-host',
  imports: [FloatingToolbar, IdleChrome],
  template: `<div class="region" nxIdleChrome>
      <nx-floating-toolbar label="檢視工具" [pinned]="pinned()">
        <button type="button" class="tool">放大</button>
      </nx-floating-toolbar>
      <p class="content">內容</p>
    </div>
    <nx-floating-toolbar class="unscoped" label="無範圍" />`,
})
class Host {
  readonly pinned = signal(false);
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  document.body.append(fixture.nativeElement);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const toolbar = root.querySelector('nx-floating-toolbar')!;
  const state = (element: Element = toolbar) => {
    fixture.detectChanges();
    return element.getAttribute('data-chrome');
  };
  const idle = (ms = 2000) => {
    fixture.detectChanges();
    vi.advanceTimersByTime(ms);
    return state();
  };
  return { fixture, host: fixture.componentInstance, root, toolbar, state, idle };
}

describe('FloatingToolbar', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
    document.body.replaceChildren();
  });

  it('names its group and follows its IdleChrome scope', () => {
    const { toolbar, state, idle } = setup();
    expect(toolbar.querySelector('[role="group"]')?.getAttribute('aria-label')).toBe('檢視工具');
    expect(state()).toBe('shown');
    expect(idle()).toBe('hidden');
  });

  it('stays while the host pins it and fades once unpinned', () => {
    const { host, state, idle } = setup();
    expect(idle()).toBe('hidden');
    host.pinned.set(true);
    expect(state()).toBe('shown');
    expect(idle(6000)).toBe('shown');
    host.pinned.set(false);
    expect(idle()).toBe('hidden');
  });

  it('stays visible without a scope', () => {
    const { root, state, idle } = setup();
    const unscoped = root.querySelector('.unscoped')!;
    idle(6000);
    expect(state(unscoped)).toBeNull();
  });
});
