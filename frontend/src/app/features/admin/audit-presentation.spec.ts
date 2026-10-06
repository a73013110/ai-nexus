import { describe, expect, it } from 'vitest';
import { auditChanges, auditResource, auditResult, auditRejected } from './audit-presentation';
describe('audit presentation', () => {
  it('preserves feature identities for grouped before and after rendering', () => {
    const changes = auditChanges(
      JSON.stringify({
        before: { featureIds: ['chat', 'admin'] },
        after: { featureIds: ['chat', 'reports'] },
      }),
    );
    expect(changes[0].featureIds).toEqual({
      before: ['chat', 'admin'],
      after: ['chat', 'reports'],
    });
  });
  it('does not label successful removal, scheduling or legacy read operations as rejected', () => {
    for (const result of [
      'created',
      'deleted',
      'soft_deleted',
      'queued',
      'read-only',
      'granted_once',
      'cancelled',
      'started',
      'restored',
    ]) {
      expect(auditRejected(result)).toBe(false);
      expect(auditResult(result)).not.toBe(result);
    }
    expect(auditRejected('executor_lost')).toBe(true);
    expect(auditRejected('unknown_access_id')).toBe(true);
  });
  it('compares nested policy changes and preserves the removal of an explicit limit', () => {
    const modelId = 'ollama/qwen3.8:27b';
    for (const nested of [true, false]) {
      const before = { allowedModelIds: ['a'], dailyTokenLimits: { [modelId]: 100000 } },
        after = { allowedModelIds: ['a'], dailyTokenLimits: null };
      const changes = auditChanges(
        JSON.stringify({
          before: { name: '舊名稱', ...(nested ? { policy: before } : before) },
          after: { name: '新名稱', ...(nested ? { policy: after } : after) },
        }),
        { [modelId]: '測試模型' },
      );
      expect(changes).toHaveLength(2);
      expect(changes.find((x) => x.key.endsWith(modelId))).toMatchObject({
        label: '每日 token 上限 · 測試模型',
        before: '100,000 tokens',
        after: '繼承設定',
      });
    }
  });
  it('supports creation and safely tolerates legacy or malformed details', () => {
    expect(auditChanges('{')).toEqual([]);
    expect(auditChanges('{"failureCode":"admin_lockout"}')).toEqual([]);
    expect(auditChanges('{"before":null,"after":{"enabled":true}}')[0].before).toBe('未設定');
    expect(auditResource('{"userId":"target"}')).toBe('target');
  });
});
