import { describe, expect, it } from 'vitest';
import type { DirectoryPersonDetailDto } from '@core/api/schema';
import { accountAlerts, accountFlags, attributeGroups } from './directory-attributes';

const NOW = Date.parse('2026-10-23T00:00:00Z');

const detail = (extra: Partial<DirectoryPersonDetailDto> = {}): DirectoryPersonDetailDto => ({
  sid: 'S-1-5-21-1-2-3-1101',
  objectGuid: null,
  dn: 'CN=林可蘿,OU=平台部,DC=example,DC=test',
  path: '平台部',
  account: 'carol.lin',
  displayName: '林可蘿',
  givenName: null,
  surname: null,
  upn: null,
  email: null,
  employeeId: null,
  description: null,
  company: null,
  department: null,
  title: null,
  manager: null,
  office: null,
  phone: null,
  mobile: null,
  accountControl: 0x200,
  accountExpires: null,
  passwordLastSet: null,
  passwordMustChange: false,
  passwordExpires: null,
  lockoutTime: null,
  badPasswordCount: null,
  badPasswordTime: null,
  lastLogon: null,
  lastLogonTimestamp: null,
  logonCount: null,
  whenCreated: null,
  whenChanged: null,
  groups: [],
  moreGroups: false,
  ...extra,
});

const item = (person: DirectoryPersonDetailDto, key: string) =>
  attributeGroups(person, NOW)
    .flatMap((group) => group.items)
    .find((entry) => entry.key === key);

describe('AD attributes', () => {
  it('splits userAccountControl into labelled flags, blocking ones first, and keeps unknown bits', () => {
    expect(accountFlags(0x10202).map((flag) => flag.label)).toEqual([
      '已停用',
      '密碼永不過期',
      '一般帳號',
    ]);
    expect(accountFlags(0x80_0010).map((flag) => flag.label)).toEqual(['已鎖定', '密碼已過期']);
    expect(accountFlags(0x200 | 0x2000_0000).at(-1)?.label).toBe('其他 0x20000000');
  });

  it('raises what stops a sign-in above the attributes', () => {
    expect(accountAlerts(detail({ accountControl: 0x10212 }), NOW).map((a) => a.label)).toEqual([
      '已停用',
      '已鎖定',
    ]);
    const forced = detail({ passwordMustChange: true, accountExpires: '2026-10-01T00:00:00Z' });
    expect(accountAlerts(forced, NOW).map((a) => a.label)).toEqual([
      '下次登入須變更密碼',
      '帳號已到期',
    ]);
  });

  it('writes "never" for the expiries AD leaves empty and the forced password change', () => {
    expect(item(detail(), 'accountExpires')?.value).toBe('永不');
    const noExpiry = detail({ accountControl: 0x10200 });
    expect(item(noExpiry, 'msDS-UserPasswordExpiryTimeComputed')?.value).toBe('永不過期');
    expect(item(detail({ passwordMustChange: true }), 'pwdLastSet')?.value).toBe(
      '下次登入時須變更',
    );
    expect(item(detail(), 'lastLogon')?.value).toBe('這台 DC 沒有紀錄');
    expect(item(detail(), 'lastLogonTimestamp')?.value).toBe('從未登入');
    const groups = detail({ moreGroups: true, groups: ['研發群組'] });
    expect(item(groups, 'memberOf')?.label).toBe('群組（1+）');
  });

  it('adds how long ago only for the last month, where the date alone is hard to judge', () => {
    const recent = detail({
      lastLogon: '2026-10-20T00:00:00Z',
      whenChanged: '2025-01-01T00:00:00Z',
    });
    expect(item(recent, 'lastLogon')?.hint).toBe('3 天前');
    expect(item(recent, 'whenChanged')?.hint).toBeUndefined();
  });
});
