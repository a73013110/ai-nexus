import { describe, expect, it } from 'vitest';
import type { AdminCatalogDto, AdminUserDto } from '@core/api/schema';
import {
  emailState,
  signInMethods,
  userAccountDraft,
  userAccountRequest,
} from './user-account-fields';
import { userFeatures } from './user-role-fields';

const user = {
  id: 'u1',
  displayName: '王小明',
  account: 'wang',
  enabled: true,
  roleIds: ['member'],
  authentication: {
    adEnabled: true,
    localEnabled: false,
    adAccount: 'wang',
    localAccount: null,
    hasLocalPassword: false,
    directoryBound: true,
  },
} as unknown as AdminUserDto;

describe('user account form', () => {
  it('starts a new user on a local password and an existing one on AD', () => {
    expect(userAccountDraft()).toMatchObject({ adEnabled: false, localEnabled: true, name: '' });
    expect(userAccountDraft(user)).toMatchObject({
      adEnabled: true,
      localEnabled: false,
      adAccount: 'wang',
      password: '',
    });
  });

  it('sends only the accounts of enabled methods, the given status and never an empty password', () => {
    const draft = { ...userAccountDraft(user), localAccount: 'wang.local' };
    expect(userAccountRequest(draft, ['member'], false)).toEqual({
      displayName: '王小明',
      enabled: false,
      adEnabled: true,
      localEnabled: false,
      adAccount: 'wang',
      localAccount: null,
      password: null,
      roleIds: ['member'],
    });
  });

  it('names the sign-in methods, AD being the default of directory users', () => {
    expect(signInMethods(user)).toBe('AD');
    expect(
      signInMethods({
        ...user,
        authentication: {
          adEnabled: true,
          localEnabled: true,
          adAccount: 'wang',
          localAccount: 'wang',
          hasLocalPassword: true,
          directoryBound: true,
        },
      }),
    ).toBe('AD · 本地密碼');
  });

  it('labels where an email came from and whether it is verified', () => {
    expect(emailState({ emailSource: 'directory', emailVerifiedAt: '2026-10-20T00:00:00Z' })).toBe(
      'AD 提供',
    );
    expect(emailState({ emailSource: 'user', emailVerifiedAt: '2026-10-20T00:00:00Z' })).toBe(
      '已驗證',
    );
    expect(emailState({ emailSource: 'admin', emailVerifiedAt: null })).toBe('管理員設定・未驗證');
    expect(emailState({ emailSource: 'user', emailVerifiedAt: null })).toBe('未驗證');
  });
});

describe('user role preview', () => {
  const catalog = {
    roles: [
      { id: 'member', name: '成員', enabled: true, groupIds: ['basic'] },
      { id: 'reviewer', name: '審閱者', enabled: true, groupIds: ['review'] },
      { id: 'retired', name: '退役', enabled: false, groupIds: ['admin'] },
    ],
    groups: [
      { id: 'basic', name: '基本', enabled: true, featureIds: ['chat'] },
      { id: 'review', name: '審閱', enabled: true, featureIds: ['repositories', 'off'] },
      { id: 'admin', name: '管理', enabled: true, featureIds: ['admin'] },
    ],
    features: [
      { id: 'chat', name: '對話', enabled: true },
      { id: 'repositories', name: '程式庫', enabled: true },
      { id: 'off', name: '停用功能', enabled: false },
      { id: 'admin', name: '平台管理', enabled: true },
    ],
    models: [],
  } as unknown as AdminCatalogDto;

  it('grants enabled features of enabled groups of enabled roles only', () => {
    const ids = (roles: string[], enabled = true) =>
      userFeatures(catalog, roles, enabled).map((item) => item.id);
    expect(ids(['member'])).toEqual(['chat']);
    expect(ids(['member', 'reviewer', 'retired'])).toEqual(['chat', 'repositories']);
    expect(ids(['member'], false)).toEqual([]);
  });
});
