import { convertToParamMap } from '@angular/router';
import { emptyMailQuery, mailFilters, mailQueryOptions } from './mail-query';

describe('mail query', () => {
  it('turns the applied query into API filters and rejects malformed categories', () => {
    const now = Date.parse('2026-10-20T08:00:00Z');
    expect(
      mailFilters({ ...emptyMailQuery(), status: 'failed', search: '  chen@company.com ' }, now),
    ).toEqual({
      status: 'failed',
      search: 'chen@company.com',
      category: undefined,
      from: undefined,
      until: undefined,
    });
    const recent = mailFilters(
      { ...emptyMailQuery(), range: { preset: '24h', from: '', to: '' } },
      now,
    );
    expect(typeof recent === 'object' && recent.from).toBe('2026-10-19T08:01:00.000Z');
    expect(mailFilters({ ...emptyMailQuery(), category: 'Account Code' }, now)).toContain('分類');
  });

  it('reads links and ignores parameters it does not understand', () => {
    const query = mailQueryOptions.fromParams!(
      convertToParamMap({
        status: 'sent',
        category: 'account.email-change-code',
        range: '7d',
        search: 'x',
      }),
      emptyMailQuery(),
    );
    expect(query).toEqual({
      status: 'sent',
      search: 'x',
      category: 'account.email-change-code',
      range: { preset: '7d', from: '', to: '' },
    });
    const ignored = mailQueryOptions.fromParams!(
      convertToParamMap({ status: 'lost', category: '<script>', range: '9y' }),
      emptyMailQuery(),
    );
    expect(ignored).toEqual(emptyMailQuery());
    expect(mailQueryOptions.toParams!(query)).toEqual({
      status: 'sent',
      search: 'x',
      category: 'account.email-change-code',
      range: '7d',
      from: undefined,
      until: undefined,
    });
  });
});
