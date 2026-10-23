import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ChangeList, type FieldChange } from './change-list';

function render(changes: FieldChange[]) {
  const fixture = TestBed.createComponent(ChangeList);
  fixture.componentRef.setInput('changes', changes);
  fixture.detectChanges();
  return [...(fixture.nativeElement as HTMLElement).querySelectorAll('.ui-change')];
}

describe('ChangeList', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('crosses out the old value, marks the new one and reads as "改為" for screen readers', () => {
    const [row] = render([{ label: '姓名', before: 'Carol', after: '林可蘿' }]);
    expect(row.querySelector('dt')?.textContent).toBe('姓名');
    expect(row.querySelector('del')?.textContent).toBe('Carol');
    expect(row.querySelector('ins')?.textContent).toBe('林可蘿');
    expect(row.querySelector('.ui-change-arrow')?.getAttribute('aria-hidden')).toBe('true');
    expect(row.querySelector('.sr-only')?.textContent).toBe('改為');
  });

  it('shows only the new value when there was none before', () => {
    const [row] = render([{ label: '通知信箱', before: null, after: 'carol@example.test' }]);
    expect(row.querySelector('del')).toBeNull();
    expect(row.querySelector('.sr-only')).toBeNull();
    expect(row.querySelector('ins')?.textContent).toBe('carol@example.test');
  });

  it('writes empty values as （無）', () => {
    const rows = render([
      { label: '職稱', before: '', after: null },
      { label: '部門', before: '研發處', after: '' },
    ]);
    expect(
      rows.map((row) => [
        row.querySelector('del')?.textContent,
        row.querySelector('ins')?.textContent,
      ]),
    ).toEqual([
      ['（無）', '（無）'],
      ['研發處', '（無）'],
    ]);
  });
});
