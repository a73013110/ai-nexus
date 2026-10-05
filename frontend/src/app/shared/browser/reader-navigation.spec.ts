import { describe, expect, it } from 'vitest';
import { readerReturnLabel, readerReturnUrl } from './reader-navigation';

describe('reader return destinations', () => {
  it('preserves the exact conversation, project and workspace filters', () => {
    for (const url of [
      '/chat/abc-123',
      '/projects/abc-123',
      '/knowledge?collection=abc-123',
      '/chat/abc-123#message-123',
    ])
      expect(readerReturnUrl(url)).toBe(url);
    expect(readerReturnLabel('/chat/abc-123')).toBe('返回對話');
    expect(readerReturnLabel('/projects/abc-123')).toBe('返回專案');
    expect(readerReturnLabel('/knowledge?collection=abc-123')).toBe('返回知識庫');
  });
  it('rejects external redirects, malformed origins and recursive reader returns', () => {
    for (const value of [
      'https://evil.invalid',
      '//evil.invalid',
      '//[',
      '/\\evil.invalid',
      'javascript:alert(1)',
      '/reader/123',
      '/login?returnUrl=https://evil.invalid',
      '/chat\n',
      null,
      {},
      '/chat?' + 'x'.repeat(2048),
    ])
      expect(readerReturnUrl(value)).toBeNull();
  });
});
