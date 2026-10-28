import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TableFilter } from './table-filter';
import type { ColumnFilter, TableColumnDef } from './table-model';

@Component({
  imports: [TableFilter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-table-filter
    [column]="column()"
    [value]="value()"
    (valueChange)="changed($event)"
  />`,
})
class Host {
  readonly column = signal<TableColumnDef>({ id: 'name', label: '名稱', filter: 'text' });
  readonly value = signal<ColumnFilter | undefined>(undefined);
  readonly changed = vi.fn((filter: ColumnFilter | null) => this.value.set(filter ?? undefined));
}

describe('TableFilter', () => {
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });

  function setup(column?: TableColumnDef) {
    const fixture = TestBed.createComponent(Host);
    if (column) fixture.componentInstance.column.set(column);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, host: fixture.componentInstance };
  }
  const type = (input: HTMLInputElement, value: string, event = 'input') => {
    input.value = value;
    input.dispatchEvent(new Event(event));
  };

  it('applies text after typing stops, or at once on Enter', () => {
    vi.useFakeTimers();
    const { root, host } = setup();
    const input = root.querySelector<HTMLInputElement>('input[type="search"]')!;
    type(input, '王');
    type(input, '王小');
    expect(host.changed).not.toHaveBeenCalled();
    vi.advanceTimersByTime(300);
    expect(host.changed).toHaveBeenCalledExactlyOnceWith({ kind: 'text', text: '王小' });

    type(input, '王小明');
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(host.changed).toHaveBeenLastCalledWith({ kind: 'text', text: '王小明' });
    vi.advanceTimersByTime(300);
    expect(host.changed).toHaveBeenCalledTimes(2);
  });

  it('toggles options and clears the column filter', () => {
    const { fixture, root, host } = setup({
      id: 'state',
      label: '狀態',
      filter: {
        options: [
          { value: 'open', label: '進行中' },
          { value: 'closed', label: '已結束' },
        ],
      },
    });
    const boxes = () => [...root.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')];
    expect(boxes()).toHaveLength(2);
    boxes()[1].click();
    fixture.detectChanges();
    expect(host.value()).toEqual({ kind: 'options', values: ['closed'] });
    boxes()[0].click();
    fixture.detectChanges();
    expect(host.value()).toEqual({ kind: 'options', values: ['closed', 'open'] });

    [...root.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('清除篩選'))!
      .click();
    expect(host.changed).toHaveBeenLastCalledWith(null);
  });

  it('keeps the other end of a range when one end changes', () => {
    const { fixture, root, host } = setup({ id: 'count', label: '數量', filter: 'number' });
    const [from, to] = root.querySelectorAll<HTMLInputElement>('input[type="number"]');
    type(from, '2', 'change');
    fixture.detectChanges();
    type(to, '9', 'change');
    fixture.detectChanges();
    expect(host.value()).toEqual({ kind: 'range', from: '2', to: '9' });
    type(from, '', 'change');
    expect(host.value()).toEqual({ kind: 'range', from: undefined, to: '9' });
  });

  it('picks dates with the shared calendar and keeps the start no later than the end', () => {
    const { fixture, root, host } = setup({ id: 'at', label: '時間', filter: 'date' });
    const pickers = root.querySelectorAll('nx-date-time-picker');
    expect(pickers).toHaveLength(2);
    expect(root.querySelector('input[type="date"]')).toBeNull();
    const [from, to] = root.querySelectorAll<HTMLInputElement>(
      'nx-date-time-picker input[type="text"]',
    );
    type(from, '2026/10/01');
    fixture.detectChanges();
    type(to, '2026/10/31');
    fixture.detectChanges();
    expect(host.value()).toEqual({ kind: 'range', from: '2026-10-01', to: '2026-10-31' });
    type(to, '2026/09/30');
    fixture.detectChanges();
    expect(host.value()).toEqual({ kind: 'range', from: '2026-10-01', to: undefined });
  });
});
