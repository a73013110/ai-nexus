import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import {
  actionLabel,
  actionsFor,
  auditFilters,
  auditQueryOptions,
  emptyAuditQuery,
} from './audit-query';

const actions = [
  {
    value: 'identity.user_access.read',
    label: '查閱使用者授權',
    category: 'access',
    module: 'identity',
  },
  {
    value: 'identity.session.signed_in',
    label: '登入工作區',
    category: 'authentication',
    module: 'identity',
  },
];
const modules = [{ value: 'identity', label: '使用者' }];
const options = auditQueryOptions(
  () => actions,
  () => modules,
);

describe('audit query', () => {
  it('lists the actions of a category', () => {
    expect(actionsFor(actions, '').length).toBe(2);
    expect(actionsFor(actions, 'access').map((action) => action.value)).toEqual([
      'identity.user_access.read',
    ]);
  });

  it('names actions and module prefixes from the catalog', () => {
    expect(actionLabel('identity.session.signed_in', actions, modules)).toBe('登入工作區');
    expect(actionLabel('identity.', actions, modules)).toBe('使用者（全部）');
    expect(actionLabel('unknown.thing.read', actions, modules)).toBe('unknown.thing.read');
  });

  it('sends results and a date range to the API', () => {
    expect(
      auditFilters({
        ...emptyAuditQuery(),
        results: ['failed'],
        feature: 'attendance.records',
        range: { preset: 'custom', from: '2026-10-01', to: '2026-10-02' },
      }),
    ).toEqual({
      category: undefined,
      search: undefined,
      action: undefined,
      feature: 'attendance.records',
      result: ['failed'],
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
    expect(read({ traceId: 'x', result: ['failed', 'nope'], from: 'today' })).toMatchObject({
      traceId: '',
      results: ['failed'],
      range: { preset: 'all' },
    });
    expect(
      read({ action: 'access-control.role.updated', feature: 'attendance.records' }),
    ).toMatchObject({
      action: 'access-control.role.updated',
      feature: 'attendance.records',
    });
    expect(read({ action: 'Bad Action', feature: '<x>' })).toMatchObject({
      action: '',
      feature: '',
    });
  });

  it('names chips from the catalog and leaves the category to the view switch', () => {
    const query = {
      ...emptyAuditQuery(),
      category: 'access',
      action: 'identity.',
      feature: 'attendance.records',
      results: ['success'],
      range: { preset: '7d', from: '', to: '' },
    };
    expect(options.chips!(query).map((chip) => chip.label)).toEqual([
      '動作：使用者（全部）',
      '功能：出勤紀錄',
      '結果：完成',
      '時間：最近 7 天',
    ]);
    expect(options.toParams!(query)).toEqual({
      category: 'access',
      action: 'identity.',
      feature: 'attendance.records',
      result: ['success'],
      range: '7d',
    });
  });
});
