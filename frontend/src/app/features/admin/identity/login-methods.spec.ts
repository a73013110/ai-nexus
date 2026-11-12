import { describe, expect, it } from 'vitest';
import { isLoginMethod, offeredMethods, passwordLabelFor } from './login-methods';

describe('login methods', () => {
  it('keeps the server order and drops methods this build does not know', () => {
    expect(offeredMethods({ mode: 'Ldap', methods: ['ad', 'passkey', 'local'] })).toEqual([
      'ad',
      'local',
    ]);
  });

  it('falls back to the directory mode when the server lists no methods', () => {
    expect(offeredMethods({ mode: 'Windows' })).toEqual(['windows']);
    expect(offeredMethods({ mode: 'Ldap', methods: null })).toEqual(['ad']);
    expect(offeredMethods({ mode: 'Ldap', methods: ['passkey'] })).toEqual(['ad']);
    expect(offeredMethods(null)).toEqual(['ad']);
  });

  it('rejects inherited and unknown names from the query string', () => {
    expect(isLoginMethod('local')).toBe(true);
    expect(isLoginMethod('toString')).toBe(false);
    expect(isLoginMethod(null)).toBe(false);
  });

  it('names the password by the directory that verifies it', () => {
    expect(passwordLabelFor('local')).toBe('本地密碼');
    expect(passwordLabelFor('ad')).toBe('AD 密碼');
    // Windows 登入沒有帳密表單，解鎖與步升驗證以 AD 密碼驗證。
    expect(passwordLabelFor('windows')).toBe('AD 密碼');
    expect(passwordLabelFor(null)).toBe('AD 密碼');
  });
});
