import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { PropertyList, type PropertyGroup } from './property-list';

const groups: PropertyGroup[] = [
  {
    id: 'identity',
    label: '身分',
    items: [
      { key: 'displayName', label: '顯示名稱', value: '林可蘿' },
      { key: 'mail', label: '信箱', value: null },
    ],
  },
  {
    id: 'account',
    label: '帳號狀態',
    note: '這一組的值來自同一台伺服器。',
    items: [
      {
        key: 'userAccountControl',
        label: '帳號控制',
        badges: [{ label: '已鎖定', tone: 'danger' }],
      },
      { key: 'objectSid', label: 'SID', value: 'S-1-5-21-1-2-3-1101', copy: true },
    ],
  },
  { id: 'groups', label: '所屬群組', items: [{ key: 'memberOf', label: '群組', list: [] }] },
];

function render() {
  const fixture = TestBed.createComponent(PropertyList);
  fixture.componentRef.setInput('label', 'AD 屬性');
  fixture.componentRef.setInput('groups', groups);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const keys = () => [...root.querySelectorAll('dt code')].map((code) => code.textContent?.trim());
  const heads = () => [...root.querySelectorAll('h5')].map((head) => head.textContent?.trim());
  return { fixture, root, keys, heads, list: fixture.componentInstance };
}

describe('PropertyList', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('groups the properties, writes a group note once and hides the ones without a value', () => {
    const { root, keys, heads, list, fixture } = render();
    expect(heads()).toEqual(['身分', '帳號狀態']);
    expect(keys()).toEqual(['displayName', 'userAccountControl', 'objectSid']);
    expect(root.querySelectorAll('.ui-props-note')).toHaveLength(1);
    expect(root.querySelector('nx-status-badge')?.textContent).toBe('已鎖定');
    expect(root.querySelector('nx-copy-value code')?.textContent).toBe('S-1-5-21-1-2-3-1101');
    expect(root.querySelector('.ui-props-blank-toggle')?.textContent).toContain('（2）');

    list.showBlank.set(true);
    fixture.detectChanges();
    expect(heads()).toEqual(['身分', '帳號狀態', '所屬群組']);
    expect(keys()).toContain('mail');
  });

  it('filters by label, source name, value or flag and marks the match', () => {
    const { root, keys, list, fixture } = render();
    list.query.set('鎖定');
    fixture.detectChanges();
    expect(keys()).toEqual(['userAccountControl']);

    list.query.set('MAIL');
    fixture.detectChanges();
    expect(keys()).toEqual(['mail']);
    expect(root.querySelector('mark')?.textContent).toBe('mail');

    list.query.set('不存在');
    fixture.detectChanges();
    expect(root.querySelector('.ui-props-empty')?.textContent).toContain(
      '沒有符合「不存在」的屬性',
    );
  });
});
