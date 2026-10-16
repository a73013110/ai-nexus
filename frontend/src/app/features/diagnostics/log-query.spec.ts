import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import {
  emptyLogQuery,
  logFilter,
  logQueryOptions,
  searchKind,
  validateLogQuery,
  type LogQuery,
} from './log-query';

const now = Date.parse('2026-10-16T00:00:00Z');
const query = (values: Partial<LogQuery>): LogQuery => ({ ...emptyLogQuery(), ...values });
const issue = 'NX-0123456789abcdef0123456789abcdef';
const trace = 'ABCDEF0123456789ABCDEF0123456789';
const guid = '6f1c2d3e-4a5b-4c6d-8e9f-0a1b2c3d4e5f';

describe('log query', () => {
  it('reads one search as an issue code, TraceId, ID or message text', () => {
    expect(searchKind(issue)).toBe('issueCode');
    expect(searchKind(trace)).toBe('traceId');
    expect(searchKind(guid)).toBe('guid');
    expect(searchKind('timeout')).toBe('text');
    expect(searchKind('  ')).toBe('');
    expect(logFilter(query({ search: issue }), now)).toMatchObject({ issueCode: issue, text: '' });
    expect(logFilter(query({ search: trace }), now)).toMatchObject({
      traceId: trace.toLowerCase(),
    });
    expect(logFilter(query({ search: guid, guid: 'jobId' }), now)).toMatchObject({
      jobId: guid,
      runId: '',
    });
    expect(logFilter(query({ search: ' timeout ' }), now)).toMatchObject({ text: 'timeout' });
  });

  it('limits message text to a day and checks issue codes', () => {
    expect(validateLogQuery(query({ search: 'timeout' }), now)).toBe('');
    expect(
      validateLogQuery(
        query({ search: 'timeout', range: { preset: '7d', from: '', to: '' } }),
        now,
      ),
    ).toContain('限一天內');
    expect(
      validateLogQuery(query({ search: trace, range: { preset: '7d', from: '', to: '' } }), now),
    ).toBe('');
    expect(validateLogQuery(query({ search: 'NX-short' }), now)).toContain('NX-');
  });

  it('presets the query from links, including the older single-ID links', () => {
    const read = (params: Record<string, string | string[]>) =>
      logQueryOptions.fromParams!(convertToParamMap(params), emptyLogQuery());
    const link = read({
      traceId: trace,
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-01T00:10:00Z',
      level: ['Error', 'Nope', 'Warning'],
    });
    expect(link.search).toBe(trace);
    expect(link.levels).toEqual(['Warning', 'Error']);
    expect(link.range).toEqual({
      preset: 'custom',
      from: '2026-10-01T08:00:00',
      to: '2026-10-01T08:10:00',
    });
    expect(read({ jobId: guid })).toMatchObject({ search: guid, guid: 'jobId' });
    expect(read({ traceId: 'not-a-trace', range: '1h' })).toMatchObject({
      search: '',
      range: { preset: '1h' },
    });
  });

  it('writes only conditions away from their defaults, and chips for them', () => {
    expect(logQueryOptions.toParams!(emptyLogQuery())).toEqual({});
    const applied = query({
      levels: ['Error'],
      search: guid,
      guid: 'jobId',
      category: ' Inference ',
    });
    expect(logQueryOptions.toParams!(applied)).toEqual({
      level: ['Error'],
      q: guid,
      match: 'jobId',
      category: 'Inference',
    });
    expect(logQueryOptions.chips!(applied).map((chip) => chip.label)).toEqual([
      '等級：Error',
      'JobId：' + guid,
      '模組 Category：Inference',
    ]);
  });
});
