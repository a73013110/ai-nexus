import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TreeSelect, type TreeSelectNode, type TreeSelectStatus } from './tree-select';

const nodes: TreeSelectNode[] = [
  { value: 'hq', label: '星河科技' },
  { value: 'rd', label: '研發處', parent: 'hq' },
  { value: 'pf', label: '平台部', parent: 'rd' },
  { value: 'old', label: '舊研發處', parent: 'hq', inactive: true },
  { value: 'old-qa', label: '測試組', parent: 'old' },
  { value: 'lab', label: '測試據點', inactive: true },
];

// 表單常把選擇器包在欄位 <label> 裡（「位置」「上層單位」）。
@Component({
  imports: [TreeSelect],
  template: `<label
    >位置<nx-tree-select
      label="位置"
      allLabel="最上層"
      [nodes]="nodes"
      [value]="value()"
      (valueChange)="value.set($event)"
  /></label>`,
})
class LabelledHost {
  readonly nodes = nodes;
  readonly value = signal('');
}

describe('TreeSelect', () => {
  beforeEach(() => {
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
  });
  afterEach(() => {
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).hidePopover;
    TestBed.resetTestingModule();
  });

  function setup(value = '', status: TreeSelectStatus = 'any', list = nodes) {
    const fixture = TestBed.createComponent(TreeSelect);
    fixture.componentRef.setInput('label', '單位');
    fixture.componentRef.setInput('allLabel', '所有單位');
    fixture.componentRef.setInput('noun', '單位');
    fixture.componentRef.setInput('nodes', list);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('status', status);
    fixture.componentRef.setInput('searchFrom', 3);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const select = fixture.componentInstance;
    const trigger = host.querySelector<HTMLButtonElement>('[role="combobox"]')!;
    const items = () => [...host.querySelectorAll<HTMLElement>('[role="treeitem"]')];
    const names = () => items().map((item) => item.querySelector('strong')!.textContent!.trim());
    const open = () => {
      trigger.click();
      fixture.detectChanges();
    };
    const key = (target: HTMLElement, key: string) => {
      target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
      fixture.detectChanges();
    };
    return { fixture, host, select, trigger, items, names, open, key };
  }

  it('shows the parents as a muted path before the name, and the full path as the title', () => {
    const { trigger, fixture } = setup('pf');
    expect(trigger.querySelector('.tree-select-path')!.textContent).toBe('星河科技 › 研發處');
    expect(trigger.querySelector('.tree-select-name')!.textContent).toBe('平台部');
    expect(trigger.title).toBe('星河科技 › 研發處 › 平台部');
    fixture.componentRef.setInput('value', '');
    fixture.detectChanges();
    expect(trigger.textContent).toContain('所有單位');
  });

  it('lists a tree with levels, keeping inactive nodes out until the viewer asks', () => {
    const { open, items, names, host, fixture } = setup('rd');
    open();
    expect(names()).toEqual(['所有單位', '星河科技', '研發處', '平台部']);
    expect(items().map((item) => item.getAttribute('aria-level'))).toEqual(['1', '1', '2', '3']);
    expect(items()[2].getAttribute('aria-selected')).toBe('true');
    const toggle = host.querySelector<HTMLInputElement>('.tree-select-footer input')!;
    toggle.click();
    fixture.detectChanges();
    expect(names()).toEqual([
      '所有單位',
      '星河科技',
      '研發處',
      '平台部',
      '舊研發處',
      '測試組',
      '測試據點',
    ]);
    expect(items()[4].querySelector('.tree-select-tag')!.textContent).toBe('停用');
    // 停用的下層繼承停用：淡色，但不另外標籤。
    expect(items()[5].classList).toContain('is-inactive');
    expect(items()[5].querySelector('.tree-select-tag')).toBeNull();
  });

  it('opens with inactive nodes listed when the chosen one is inactive', () => {
    const { open, names } = setup('old-qa');
    open();
    expect(names()).toContain('測試組');
  });

  it('lets the developer allow only active nodes, without a toggle', () => {
    const { open, names, host } = setup('', 'active');
    open();
    expect(names()).toEqual(['所有單位', '星河科技', '研發處', '平台部']);
    expect(host.querySelector('.tree-select-footer')).toBeNull();
  });

  it('lets the developer allow only inactive nodes, keeping active parents as a path', () => {
    const { open, items, names, select } = setup('', 'inactive');
    const emitted: string[] = [];
    select.valueChange.subscribe((value) => emitted.push(value));
    open();
    expect(names()).toEqual(['所有單位', '星河科技', '舊研發處', '測試組', '測試據點']);
    expect(items()[1].getAttribute('aria-disabled')).toBe('true');
    select.choose(1);
    expect(emitted).toEqual([]);
    select.choose(3);
    expect(emitted).toEqual(['old-qa']);
  });

  it('shows disabled nodes without letting them be chosen', () => {
    const list = nodes.map((node) => (node.value === 'rd' ? { ...node, disabled: true } : node));
    const { open, items, select, fixture } = setup('', 'any', list);
    fixture.componentRef.setInput('allDisabled', true);
    fixture.detectChanges();
    const emitted: string[] = [];
    select.valueChange.subscribe((value) => emitted.push(value));
    open();
    expect(items()[0].getAttribute('aria-disabled')).toBe('true');
    expect(items()[2].getAttribute('aria-disabled')).toBe('true');
    select.choose(0);
    select.choose(2);
    select.choose(3);
    expect(emitted).toEqual(['pf']);
  });

  it('flattens while searching, shows where each match sits and counts hidden inactive matches', () => {
    const { open, items, host, select, fixture } = setup();
    open();
    select.filter('研發');
    fixture.detectChanges();
    expect(items()).toHaveLength(1);
    expect(items()[0].querySelector('small')!.textContent).toBe('星河科技');
    expect(items()[0].querySelector('mark')!.textContent).toBe('研發');
    select.filter('測試');
    fixture.detectChanges();
    expect(items()).toHaveLength(0);
    expect(host.querySelector('.tree-select-empty')!.textContent).toContain('另有 2 個停用的單位');
  });

  it('moves with the arrows, collapses and expands with left and right, and chooses with Enter', () => {
    const { open, names, host, select, key } = setup('rd');
    const emitted: string[] = [];
    select.valueChange.subscribe((value) => emitted.push(value));
    open();
    const search = host.querySelector<HTMLInputElement>('input[type="search"]')!;
    expect(search.getAttribute('aria-activedescendant')).toBe(`${select.id}-2`);
    key(search, 'ArrowLeft');
    expect(names()).toEqual(['所有單位', '星河科技', '研發處']);
    key(search, 'ArrowRight');
    expect(names()).toEqual(['所有單位', '星河科技', '研發處', '平台部']);
    key(search, 'ArrowRight');
    expect(search.getAttribute('aria-activedescendant')).toBe(`${select.id}-3`);
    key(search, 'ArrowLeft');
    expect(search.getAttribute('aria-activedescendant')).toBe(`${select.id}-2`);
    key(search, 'ArrowDown');
    key(search, 'Enter');
    expect(emitted).toEqual(['pf']);
  });

  it('opens and searches when the viewer types on the trigger', () => {
    const { trigger, select, key } = setup();
    key(trigger, '平');
    expect(select.opened()).toBe(true);
    expect(select.query()).toBe('平');
  });

  // jsdom 把有 tabindex 的元素當成互動內容，不會重現 Chrome 的 label 轉送，這裡直接看點擊有沒有被攔下。
  it('keeps a wrapping field label from turning clicks on rows into a trigger click', () => {
    const fixture = TestBed.createComponent(LabelledHost);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const trigger = host.querySelector<HTMLButtonElement>('[role="combobox"]')!;
    const click = (target: Element) => {
      const event = new MouseEvent('click', { bubbles: true, cancelable: true });
      target.dispatchEvent(event);
      fixture.detectChanges();
      return event.defaultPrevented;
    };
    host.querySelector('label')!.click();
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    const hq = host.querySelectorAll<HTMLElement>('[role="treeitem"]')[1];
    expect(click(hq.querySelector('.tree-select-twisty')!)).toBe(true);
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(click(hq)).toBe(true);
    expect(fixture.componentInstance.value()).toBe('hq');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
  });
});
