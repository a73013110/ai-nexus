import { convertToParamMap } from '@angular/router';
import { queryDefaults, readQuery, writeQuery } from '@shared/ui/query-state';
import { mailFilters, mailQueryOptions } from './mail-query';

const empty = () => queryDefaults(mailQueryOptions);

describe('mail query', () => {
  it('turns the applied query into API filters and rejects malformed categories', () => {
    const now = Date.parse('2026-10-20T08:00:00Z');
    expect(
      mailFilters({ ...empty(), status: 'failed', search: '  chen@company.com ' }, now),
    ).toEqual({
      status: 'failed',
      search: 'chen@company.com',
      category: undefined,
      from: undefined,
      until: undefined,
    });
    const recent = mailFilters({ ...empty(), range: { preset: '24h', from: '', to: '' } }, now);
    expect(typeof recent === 'object' && recent.from).toBe('2026-10-19T08:01:00.000Z');
    expect(mailFilters({ ...empty(), category: 'Account Code' }, now)).toContain('分類');
  });

  it('reads links and ignores parameters it does not understand', () => {
    const query = readQuery(
      mailQueryOptions,
      convertToParamMap({
        status: 'sent',
        category: 'account.email-change-code',
        range: '7d',
        search: 'x',
      }),
    );
    expect(query).toEqual({
      status: 'sent',
      search: 'x',
      category: 'account.email-change-code',
      range: { preset: '7d', from: '', to: '' },
    });
    const ignored = readQuery(
      mailQueryOptions,
      convertToParamMap({ status: 'lost', category: '<script>', range: '9y' }),
    );
    expect(ignored).toEqual(empty());
    expect(writeQuery(mailQueryOptions, query)).toEqual({
      status: 'sent',
      search: 'x',
      category: 'account.email-change-code',
      range: '7d',
      from: undefined,
      until: undefined,
    });
  });
});
