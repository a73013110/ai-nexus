import { describe, expect, it } from 'vitest';
import { presenceFeature } from './browser-presence';
describe('presence page vocabulary', () => {
  it('retains only the catalog id and discards resource identifiers and query strings', () => {
    expect(presenceFeature('/work/chat/private-title?prompt=private')).toBe('chat');
    expect(presenceFeature('/admin/monitoring?minutes=15')).toBe('monitoring');
    expect(presenceFeature('/admin/audit?search=private-user')).toBe('audit');
    expect(presenceFeature('/admin/diagnostics/logs?traceId=private-trace')).toBe(
      'diagnostics.logs',
    );
    expect(presenceFeature('/admin/access-control/roles')).toBe('access-control.roles');
    expect(presenceFeature('/work/knowledge/retrieval')).toBe('knowledge.retrieval');
    expect(presenceFeature('/work/knowledge/collection-id')).toBe('knowledge');
    expect(presenceFeature('/work/sharing/reader/recipient-id/document-id')).toBe('sharing.reader');
    expect(presenceFeature('/settings')).toBe('settings');
    expect(presenceFeature('/admin/audit-other')).toBeNull();
    expect(presenceFeature('/admin/identity')).toBeNull();
    expect(presenceFeature('/login?returnUrl=/work/chat')).toBeNull();
    expect(presenceFeature('/unknown-private-page')).toBeNull();
  });
});
