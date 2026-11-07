import { convertToParamMap } from '@angular/router';
import { queryChips, queryDefaults, readQuery, writeQuery } from '@shared/ui/query-state';
import { describe, expect, it } from 'vitest';
import {
  actionLabel,
  actionOptions,
  actionsFor,
  auditFilters,
  auditQueryOptions,
  moduleLabel,
  moduleOptions,
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
  {
    value: 'chat.message.sent',
    label: '送出訊息',
    category: 'activity',
    module: 'chat',
  },
];
const modules = [
  { value: 'identity', label: '使用者' },
  { value: 'chat', label: '對話' },
];
const options = auditQueryOptions(
  () => actions,
  () => modules,
);
const empty = () => queryDefaults(options);

describe('audit query', () => {
  it('lists the actions of a category and module', () => {
    expect(actionsFor(actions, '').length).toBe(3);
    expect(actionsFor(actions, 'access').map((action) => action.value)).toEqual([
      'identity.user_access.read',
    ]);
    expect(actionsFor(actions, '', 'chat').map((action) => action.value)).toEqual([
      'chat.message.sent',
    ]);
  });

  it('offers only modules with actions in the category, searchable by code', () => {
    expect(moduleOptions(actions, modules, '')).toEqual([
      { value: '', label: '所有模組' },
      { value: 'identity', label: '使用者', keywords: 'identity' },
      { value: 'chat', label: '對話', keywords: 'chat' },
    ]);
    expect(moduleOptions(actions, modules, 'activity').map((option) => option.value)).toEqual([
      '',
      'chat',
    ]);
  });

  it('groups actions by module until a module is chosen', () => {
    expect(actionOptions(actions, modules, '', '').map((option) => option.group)).toEqual([
      undefined,
      '使用者',
      '使用者',
      '對話',
    ]);
    expect(actionOptions(actions, modules, '', 'identity')).toEqual([
      { value: '', label: '所有動作' },
      {
        value: 'identity.user_access.read',
        label: '查閱使用者授權',
        keywords: 'identity.user_access.read',
        group: undefined,
      },
      {
        value: 'identity.session.signed_in',
        label: '登入工作區',
        keywords: 'identity.session.signed_in',
        group: undefined,
      },
    ]);
  });

  it('names actions and modules from the catalog', () => {
    expect(actionLabel('identity.session.signed_in', actions)).toBe('登入工作區');
    expect(actionLabel('unknown.thing.read', actions)).toBe('unknown.thing.read');
    expect(moduleLabel('chat', modules)).toBe('對話');
    expect(moduleLabel('retired', modules)).toBe('retired');
  });

  it('sends results and a date range to the API', () => {
    expect(
      auditFilters({
        ...empty(),
        results: ['failed'],
        feature: 'attendance.records',
        range: { preset: 'custom', from: '2026-10-01', to: '2026-10-02' },
      }),
    ).toEqual({
      category: undefined,
      search: undefined,
      module: undefined,
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
      readQuery(options, convertToParamMap(params));
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
    // 舊連結的模組前綴（`identity.`）不是動作，改用 module。
    expect(read({ action: 'identity.', module: 'Identity' })).toMatchObject({
      action: '',
      module: '',
    });
    expect(read({ module: 'access-control' }).module).toBe('access-control');
  });

  it('names chips from the catalog and leaves the category to the view switch', () => {
    const query = {
      ...empty(),
      category: 'access',
      module: 'identity',
      action: 'identity.user_access.read',
      feature: 'attendance.records',
      results: ['success'],
      range: { preset: '7d', from: '', to: '' },
    };
    expect(queryChips(options, query).map((chip) => chip.label)).toEqual([
      '模組：使用者',
      '動作：查閱使用者授權',
      '功能：出勤紀錄',
      '結果：完成',
      '時間：最近 7 天',
    ]);
    expect(writeQuery(options, query)).toEqual({
      category: 'access',
      module: 'identity',
      action: 'identity.user_access.read',
      feature: 'attendance.records',
      result: ['success'],
      range: '7d',
    });
  });
});
