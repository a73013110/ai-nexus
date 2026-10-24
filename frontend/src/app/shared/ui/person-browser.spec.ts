import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { PersonBrowser, type BrowsePerson } from './person-browser';

@Component({
  selector: 'nx-person-browser-host',
  imports: [PersonBrowser],
  template: `<nx-person-browser
    label="可加入的人員"
    emptyText="名單是空的。"
    [people]="people()"
    [selected]="selected()"
    [loading]="loading()"
    (selectedChange)="selected.set($event)"
  />`,
})
class Host {
  readonly people = signal<BrowsePerson[]>([
    { id: 'a', name: '張育誠', detail: 'chang.yc', group: '尚未分派部門' },
    { id: 'b', name: '林佳穎', detail: 'lin.jy', group: '業務部' },
    { id: 'c', name: '陳雅婷', detail: 'chen.yt', group: '工程部' },
    { id: 'd', name: '林建宏', detail: 'lin.jh', group: '業務部' },
  ]);
  readonly selected = signal<string[]>([]);
  readonly loading = signal(false);
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const browser = fixture.debugElement.children[0].componentInstance as PersonBrowser;
  const headings = () =>
    [...element.querySelectorAll('.choice-group h3')].map((h) => h.textContent);
  const boxes = () => [...element.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')];
  return { fixture, host: fixture.componentInstance, element, browser, headings, boxes };
}

describe('PersonBrowser', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('groups everyone in first-appearance order under a named list', () => {
    const { element, headings, boxes } = setup();
    expect(element.querySelector('nx-choice-groups')!.getAttribute('aria-label')).toBe(
      '可加入的人員',
    );
    expect(headings()).toEqual(['尚未分派部門', '業務部', '工程部']);
    expect(boxes()).toHaveLength(4);
    expect(element.querySelector('input[type="search"], input')!.getAttribute('aria-label')).toBe(
      '篩選人員',
    );
  });

  it('filters by name, account or group and keeps hidden picks selected', () => {
    const { fixture, host, browser, headings, boxes } = setup();
    boxes()[1].click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['b']);

    browser.text.set('chen');
    fixture.detectChanges();
    expect(headings()).toEqual(['工程部']);
    boxes()[0].click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['b', 'c']);

    browser.text.set('');
    browser.group.set('業務部');
    fixture.detectChanges();
    expect(headings()).toEqual(['業務部']);
    expect(browser.groupOptions().map((option) => option.label)).toEqual([
      '全部（4）',
      '尚未分派部門（1）',
      '業務部（2）',
      '工程部（1）',
    ]);
  });

  it('tells an empty roster apart from a filter with no match, and shows a skeleton while loading', () => {
    const { fixture, host, element, browser } = setup();
    browser.text.set('不存在');
    fixture.detectChanges();
    expect(element.querySelector('[role="status"]')!.textContent).toContain('沒有符合篩選條件的人');

    host.people.set([]);
    fixture.detectChanges();
    expect(element.querySelector('[role="status"]')!.textContent).toContain('名單是空的。');

    host.loading.set(true);
    fixture.detectChanges();
    expect(element.querySelector('.person-browser-skeleton')).not.toBeNull();
    expect(element.querySelector('nx-choice-groups')).toBeNull();
  });
});
