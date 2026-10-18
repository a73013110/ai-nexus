import { describe, expect, it } from 'vitest';
import { isLoginMethod, offeredMethods } from './login-methods';

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
});
