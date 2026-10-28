import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceSession } from '@core/auth/workspace-session';
import {
  applyLocal,
  cursorTable,
  describeFilter,
  endpointTable,
  filterParams,
  localTable,
  type TableColumnDef,
  type TableRequestQuery,
} from './table-model';

interface Row {
  id: number;
  name: string;
  count: number | null;
  state: 'open' | 'closed';
}

const rows: Row[] = [
  { id: 1, name: '王小明', count: 3, state: 'open' },
  { id: 2, name: 'Alice', count: null, state: 'closed' },
  { id: 3, name: 'bob', count: 10, state: 'open' },
  { id: 4, name: 'Item 2', count: 3, state: 'closed' },
];

const columns: TableColumnDef<Row>[] = [
  { id: 'name', label: '名稱', sort: true, filter: 'text' },
  { id: 'count', label: '數量', sort: 'desc', filter: 'number', numeric: true },
  {
    id: 'state',
    label: '狀態',
    filter: {
      options: [
        { value: 'open', label: '進行中' },
        { value: 'closed', label: '已結束' },
      ],
    },
  },
  { id: 'at', label: '時間', filter: 'date' },
];

function inContext<T>(create: () => T) {
  const session = {
    auth: { generation: signal(1) },
    load: vi.fn(async () => true),
    has: () => true,
  };
  TestBed.configureTestingModule({ providers: [{ provide: WorkspaceSession, useValue: session }] });
  const value = TestBed.runInInjectionContext(create);
  const settle = async () => {
    TestBed.tick();
    await TestBed.inject(ApplicationRef).whenStable();
  };
  return { value, settle };
}

describe('applyLocal', () => {
  it('sorts with nulls first ascending and keeps the original order for ties', () => {
    const ids = (sorted: Row[]) => sorted.map((row) => row.id);
    expect(ids(applyLocal(rows, columns, { column: 'count', direction: 'asc' }, {}))).toEqual([
      2, 1, 4, 3,
    ]);
    expect(ids(applyLocal(rows, columns, { column: 'count', direction: 'desc' }, {}))).toEqual([
      3, 1, 4, 2,
    ]);
    expect(ids(applyLocal(rows, columns, null, {}))).toEqual([1, 2, 3, 4]);
  });

  it('filters text case-insensitively, options by value and numbers by inclusive range', () => {
    const ids = (filters: Parameters<typeof applyLocal>[3]) =>
      applyLocal(rows, columns, null, filters).map((row) => row.id);
    expect(ids({ name: { kind: 'text', text: ' B ' } })).toEqual([3]);
    expect(ids({ state: { kind: 'options', values: ['closed'] } })).toEqual([2, 4]);
    expect(ids({ count: { kind: 'range', from: '3', to: '3' } })).toEqual([1, 4]);
    expect(
      ids({ count: { kind: 'range', from: '1' }, state: { kind: 'options', values: ['open'] } }),
    ).toEqual([1, 3]);
  });

  it('reads the value from the column when the row has no matching property', () => {
    const custom: TableColumnDef<Row>[] = [
      { id: 'label', label: '標籤', sort: true, value: (row) => row.name.length },
    ];
    expect(
      applyLocal(rows, custom, { column: 'label', direction: 'asc' }, {}).map((row) => row.id),
    ).toEqual([1, 3, 2, 4]);
  });
});

describe('filterParams', () => {
  it('writes the backend filter syntax, with the date end as the next local midnight', () => {
    const params = filterParams(columns as TableColumnDef<unknown>[], {
      name: { kind: 'text', text: ' a:b ' },
      state: { kind: 'options', values: ['open', 'closed'] },
      count: { kind: 'range', to: '5' },
      at: { kind: 'range', from: '2026-10-01', to: '2026-10-01' },
    });
    expect(params.slice(0, 3)).toEqual(['name:has:a:b', 'state:in:open,closed', 'count:le:5']);
    expect(params[3]).toBe(`at:ge:${new Date(2026, 9, 1).toISOString()}`);
    expect(params[4]).toBe(`at:lt:${new Date(2026, 9, 2).toISOString()}`);
  });

  it('describes a filter with the option labels for its chip', () => {
    expect(describeFilter(columns[2], { kind: 'options', values: ['open', 'closed'] })).toBe(
      '狀態：進行中、已結束',
    );
    expect(describeFilter(columns[1], { kind: 'range', from: '2' })).toBe('數量：2～');
    expect(
      describeFilter(columns[3], { kind: 'range', from: '2026-10-01', to: '2026-10-31' }),
    ).toBe('時間：2026/10/01～2026/10/31');
  });
});

describe('localTable', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('pages, sorts and filters in memory and starts over on the first page', () => {
    const navigated = vi.fn();
    const { value: table } = inContext(() =>
      localTable({ rows: () => rows, columns, pageSize: 25, pageSizes: [2, 25], navigated }),
    );
    table.resize(2);
    expect(table.rows().map((row) => row.id)).toEqual([1, 2]);
    expect(table.hasNext()).toBe(true);
    table.turn(1);
    expect(table.rows().map((row) => row.id)).toEqual([3, 4]);
    expect(table.hasNext()).toBe(false);

    table.toggleSort('count');
    expect(table.sort()).toEqual({ column: 'count', direction: 'desc' });
    expect(table.pageIndex()).toBe(0);
    expect(table.rows().map((row) => row.id)).toEqual([3, 1]);
    table.toggleSort('count');
    expect(table.sort()?.direction).toBe('asc');

    table.setFilter('state', { kind: 'options', values: ['open'] });
    expect(table.total()).toBe(2);
    expect(table.chips().map((chip) => chip.label)).toEqual(['狀態：進行中']);
    table.chips()[0].remove();
    expect(table.total()).toBe(4);
    expect(navigated).toHaveBeenCalledTimes(6);
  });

  it('ignores columns without sort or filter and empty filter values', () => {
    const { value: table } = inContext(() => localTable({ rows: () => rows, columns }));
    table.toggleSort('state');
    table.setFilter('name', { kind: 'text', text: '  ' });
    expect(table.sort()).toBeNull();
    expect(table.filters()).toEqual({});
    expect(table.revision()).toBe(0);
  });
});

describe('endpointTable', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('sorts, filters and pages locally once the endpoint returns everything', async () => {
    const params = signal({ q: 'a' });
    const load = vi.fn(async (_params: { q: string }, _table: TableRequestQuery) => ({
      items: rows,
      total: rows.length,
      offset: 0,
      complete: true,
    }));
    const { value: table, settle } = inContext(() =>
      endpointTable({ params, load, columns, pageSize: 2, pageSizes: [2] }),
    );
    await settle();
    expect(load).toHaveBeenCalledWith({ q: 'a' }, { offset: 0, limit: 2 }, expect.anything());
    expect(table.rows().map((row) => row.id)).toEqual([1, 2]);
    expect(table.total()).toBe(4);
    expect(table.local()).toBe(true);

    table.turn(1);
    table.toggleSort('count');
    table.setFilter('state', { kind: 'options', values: ['open'] });
    await settle();
    expect(load).toHaveBeenCalledTimes(1);
    expect(table.rows().map((row) => row.id)).toEqual([3, 1]);

    params.set({ q: 'b' });
    await settle();
    expect(load).toHaveBeenCalledTimes(2);
    expect(load.mock.calls[1][1]).toEqual({
      offset: 0,
      limit: 2,
      sort: 'count:desc',
      filter: ['state:in:open'],
    });
  });

  it('asks the endpoint for each page while the data is too large to send at once', async () => {
    const load = vi.fn(async (_params: true, table: TableRequestQuery) => ({
      items: rows.slice(table.offset, table.offset + table.limit),
      total: 4,
      offset: table.offset,
      complete: false,
    }));
    const { value: table, settle } = inContext(() =>
      endpointTable({
        params: () => true as const,
        load,
        columns,
        sort: { column: 'count', direction: 'desc' },
        pageSize: 2,
        pageSizes: [2],
      }),
    );
    await settle();
    expect(table.local()).toBe(false);
    table.turn(1);
    await settle();
    expect(load.mock.calls.map((call) => call[1])).toEqual([
      { offset: 0, limit: 2, sort: 'count:desc' },
      { offset: 2, limit: 2, sort: 'count:desc' },
    ]);
    expect(table.rows().map((row) => row.id)).toEqual([3, 4]);
    expect(table.hasNext()).toBe(false);
  });

  it('shows loading again when the last result was empty, but not over existing rows', async () => {
    const params = signal('none');
    // 只有第一次換條件的讀取停住，讓測試看得到讀取中的狀態；之後的重讀直接完成。
    let release!: () => void;
    let gate: Promise<void> | undefined = new Promise<void>((resolve) => (release = resolve));
    const load = vi.fn(async (query: string) => {
      if (query === 'slow') await gate;
      const items = query === 'none' ? [] : rows;
      return { items, total: items.length, offset: 0, complete: true };
    });
    const { value: table, settle } = inContext(() =>
      endpointTable({ params, load, columns, pageSize: 10, pageSizes: [10] }),
    );
    await settle();
    expect(table.rows()).toEqual([]);
    expect(table.loading()).toBe(false);

    // 查無資料後再查：讀取中要畫骨架，空狀態不能留在畫面上。
    params.set('slow');
    TestBed.tick();
    await vi.waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    expect(table.loading()).toBe(true);
    release();
    gate = undefined;
    await settle();
    expect(table.loading()).toBe(false);

    // 已有列時的重讀只淡化（refreshing），不換成骨架。
    table.reload();
    TestBed.tick();
    expect(table.refreshing()).toBe(true);
    expect(table.loading()).toBe(false);
    await settle();
  });
});

describe('cursorTable', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads the next page with the last cursor and the previous one with the remembered cursor', async () => {
    const load = vi.fn(
      async (_params: true, table: TableRequestQuery & { cursor?: string | number }) => {
        const start = typeof table.cursor === 'number' ? table.cursor : 0;
        const items = rows.slice(start, start + table.limit);
        return { items, next: start + table.limit < rows.length ? start + table.limit : null };
      },
    );
    const { value: table, settle } = inContext(() =>
      cursorTable({ params: () => true as const, load, columns, pageSize: 2, pageSizes: [2] }),
    );
    await settle();
    expect(table.mode()).toBe('cursor');
    expect(table.hasNext()).toBe(true);
    table.turn(1);
    await settle();
    expect(table.rows().map((row) => row.id)).toEqual([3, 4]);
    expect(table.hasNext()).toBe(false);
    table.turn(-1);
    await settle();
    expect(load.mock.calls.map((call) => call[1].cursor)).toEqual([undefined, 2, undefined]);
    expect(table.pageIndex()).toBe(0);
  });
});
