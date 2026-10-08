import { describe, expect, it } from 'vitest';
import { presenceFeature } from './browser-presence';
describe('presence page vocabulary', () => {
  it('retains only the feature and discards resource identifiers and query strings', () => {
    expect(presenceFeature('/chat/private-title?prompt=private')).toBe('chat');
    expect(presenceFeature('/admin/monitoring?minutes=15')).toBe('monitoring');
    expect(presenceFeature('/admin/audit?search=private-user')).toBe('audit');
    expect(presenceFeature('/admin/logs?traceId=private-trace')).toBe('logs');
    expect(presenceFeature('/admin/audit-other')).toBe('admin');
    expect(presenceFeature('/reader/share/recipient-id/document-id')).toBe('reader');
    expect(presenceFeature('/login?returnUrl=/chat')).toBeNull();
    expect(presenceFeature('/unknown-private-page')).toBeNull();
  });
});
