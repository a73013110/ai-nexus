import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { actionsFor, auditFilters, auditQueryOptions, emptyAuditQuery } from './audit-query';

const actions = [
  { value: 'admin.', label: '平台管理', categories: ['administration', 'access'] },
  { value: 'identity.login', label: '登入', categories: ['authentication'] },
];
const options = auditQueryOptions(() => actions);

describe('audit query', () => {
  it('lists the actions of a category', () => {
    expect(actionsFor(actions, '').length).toBe(2);
    expect(actionsFor(actions, 'access').map((action) => action.value)).toEqual(['admin.']);
  });

  it('sends results and a date range to the API', () => {
    expect(
      auditFilters({
        ...emptyAuditQuery(),
        results: ['failed', 'read'],
        range: { preset: 'custom', from: '2026-10-01', to: '2026-10-02' },
      }),
    ).toEqual({
      category: undefined,
      search: undefined,
      action: undefined,
      result: ['failed', 'read'],
      traceId: undefined,
      from: '2026-09-30T16:00:00.000Z',
      until: '2026-10-02T16:00:00.000Z',
    });
  });

  it('reads links from user details and system logs', () => {
    const read = (params: Record<string, string | string[]>) =>
      options.fromParams!(convertToParamMap(params), emptyAuditQuery());
    expect(read({ search: 'user-1', category: 'access' })).toMatchObject({
      search: 'user-1',
      category: 'access',
    });
    expect(read({ traceId: 'ABCDEF0123456789ABCDEF0123456789' }).traceId).toBe(
      'abcdef0123456789abcdef0123456789',
    );
    expect(read({ traceId: 'x', result: ['read', 'nope'], from: 'today' })).toMatchObject({
      traceId: '',
      results: ['read'],
      range: { preset: 'all' },
    });
  });

  it('names chips from the catalog and leaves the category to the view switch', () => {
    const query = {
      ...emptyAuditQuery(),
      category: 'access',
      action: 'admin.',
      results: ['success'],
      range: { preset: '7d', from: '', to: '' },
    };
    expect(options.chips!(query).map((chip) => chip.label)).toEqual([
      '動作：平台管理',
      '結果：已受理／完成',
      '時間：最近 7 天',
    ]);
    expect(options.toParams!(query)).toEqual({
      category: 'access',
      action: 'admin.',
      result: ['success'],
      range: '7d',
    });
  });
});
