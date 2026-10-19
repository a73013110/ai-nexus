import { describe, expect, it } from 'vitest';
import { readerReturnLabel, readerReturnUrl } from './reader-navigation';

describe('reader return destinations', () => {
  it('preserves the exact conversation, project and workspace filters', () => {
    for (const url of [
      '/work/chat/abc-123',
      '/work/projects/abc-123',
      '/work/knowledge?collection=abc-123',
      '/work/chat/abc-123#message-123',
      '/admin/quality/retrieval',
    ])
      expect(readerReturnUrl(url)).toBe(url);
    expect(readerReturnLabel('/work/chat/abc-123')).toBe('返回對話');
    expect(readerReturnLabel('/work/projects/abc-123')).toBe('返回專案');
    expect(readerReturnLabel('/work/knowledge/abc-123')).toBe('返回知識庫');
    expect(readerReturnLabel('/work/dashboard')).toBe('前往總覽');
    expect(readerReturnLabel('/elsewhere')).toBe('返回上一頁');
  });
  it('rejects external redirects, malformed origins and recursive reader returns', () => {
    for (const value of [
      'https://evil.invalid',
      '//evil.invalid',
      '//[',
      '/\\evil.invalid',
      'javascript:alert(1)',
      '/work/knowledge/reader/123',
      '/work/sharing/reader/share-1/123',
      '/work/repositories/team/nexus/code',
      '/work/chatter',
      '/chat/abc-123',
      '/login?returnUrl=https://evil.invalid',
      '/work/chat\n',
      null,
      {},
      '/work/chat?' + 'x'.repeat(2048),
    ])
      expect(readerReturnUrl(value)).toBeNull();
  });
});
