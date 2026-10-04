import { describe, expect, it } from 'vitest';
import { auditChanges, auditResource } from './audit-presentation';
describe('audit presentation', () => {
  it('compares nested policy changes and preserves the removal of an explicit limit', () => {
    const changes = auditChanges(
      JSON.stringify({
        before: { name: '舊名稱', policy: { dailyRequestLimit: 1, allowedModelIds: ['a'] } },
        after: { name: '新名稱', policy: { dailyRequestLimit: null, allowedModelIds: ['a'] } },
      }),
    );
    expect(changes).toHaveLength(2);
    expect(changes.find((x) => x.key === 'policy.dailyRequestLimit')?.after).toBe('未設定');
  });
  it('supports creation and safely tolerates legacy or malformed details', () => {
    expect(auditChanges('{')).toEqual([]);
    expect(auditChanges('{"failureCode":"admin_lockout"}')).toEqual([]);
    expect(auditChanges('{"before":null,"after":{"enabled":true}}')[0].before).toBe('未設定');
    expect(auditResource('{"userId":"target"}')).toBe('target');
  });
});
