import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ActionMenu, type MenuAction } from './action-menu';

@Component({
  selector: 'nx-action-menu-host',
  imports: [ActionMenu],
  template: `<nx-action-menu
    label="範例操作"
    [items]="items()"
    [busy]="busy()"
    (action)="chosen.push($event)"
  />`,
})
class Host {
  readonly busy = signal(false);
  readonly items = signal<MenuAction[]>([
    { id: 'export', label: '匯出', icon: 'download' },
    { id: 'copy', label: '複製', icon: 'copy' },
  ]);
  readonly chosen: string[] = [];
}

describe('ActionMenu', () => {
  beforeEach(() => {
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
  });
  afterEach(() => {
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).hidePopover;
    TestBed.resetTestingModule();
  });

  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector<HTMLButtonElement>('button[aria-haspopup]')!;
    const item = (label: string) =>
      Array.from(element.querySelectorAll<HTMLButtonElement>('[role="menuitem"]')).find((button) =>
        button.textContent?.includes(label),
      )!;
    return { fixture, host: fixture.componentInstance, trigger, item };
  }

  it('spins the trigger and keeps the menu shut while an action from it runs', () => {
    const { fixture, host, trigger } = setup();
    host.busy.set(true);
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-busy')).toBe('true');
    expect(trigger.getAttribute('aria-disabled')).toBe('true');
    expect(trigger.disabled).toBe(false);
    expect(trigger.querySelector('nx-icon')!.classList).toContain('is-spinning');
    trigger.click();
    expect(HTMLElement.prototype.showPopover).not.toHaveBeenCalled();

    host.busy.set(false);
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-busy')).toBeNull();
    trigger.click();
    expect(HTMLElement.prototype.showPopover).toHaveBeenCalled();
  });

  it('ignores a busy item but still runs the others', () => {
    const { fixture, host, item } = setup();
    host.items.update((items) => items.map((entry) => ({ ...entry, busy: entry.id === 'export' })));
    fixture.detectChanges();
    const busy = item('匯出');
    expect(busy.getAttribute('aria-busy')).toBe('true');
    expect(busy.querySelector('nx-icon')!.classList).toContain('is-spinning');
    busy.click();
    expect(host.chosen).toEqual([]);
    item('複製').click();
    expect(host.chosen).toEqual(['copy']);
  });
});
