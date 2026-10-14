import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { Tabs, type TabItem } from './tabs';

@Component({
  selector: 'nx-tabs-host',
  imports: [Tabs],
  template: `<nx-tabs label="示範分類" [items]="items()" value="account" />`,
})
class Host {
  readonly items = signal<TabItem[]>([
    { value: 'account', label: '帳號' },
    { value: 'roles', label: '角色', count: 3 },
  ]);
}

describe('Tabs', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('marks a tab with unsaved changes by a dot and in its name', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const tabs = () => [...(fixture.nativeElement as HTMLElement).querySelectorAll('[role=tab]')];
    expect(tabs()[0].getAttribute('aria-label')).toBeNull();
    expect(tabs()[0].querySelector('.tab-dirty')).toBeNull();
    expect(tabs()[1].getAttribute('aria-label')).toBe('角色，3 則');

    fixture.componentInstance.items.set([
      { value: 'account', label: '帳號', dirty: true },
      { value: 'roles', label: '角色', count: 3, dirty: true },
    ]);
    fixture.detectChanges();
    expect(tabs()[0].getAttribute('aria-label')).toBe('帳號（有未儲存的變更）');
    expect(tabs()[0].querySelector('.tab-dirty')?.getAttribute('aria-hidden')).toBe('true');
    expect(tabs()[1].getAttribute('aria-label')).toBe('角色，3 則（有未儲存的變更）');
  });
});
