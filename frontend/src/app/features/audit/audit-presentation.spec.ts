import { describe, expect, it } from 'vitest';
import {
  auditChanges,
  auditDetails,
  auditResource,
  auditResult,
  auditRejected,
} from './audit-presentation';
describe('audit presentation', () => {
  it('presents model policy arrays, dictionary keys and missing models without exposing IDs', () => {
    const id = 'ollama/hf.co/provider/private-model:Q3_K_XL';
    const json = JSON.stringify({
      before: { allowedModelIds: [id], dailyTokenLimits: { [id]: 100 } },
      after: { allowedModelIds: ['removed/model'], dailyTokenLimits: { 'removed/model': 200 } },
    });
    const names = { [id]: '本地助理' };
    const changes = auditChanges(json, names);
    expect(changes.find((x) => x.key === 'allowedModelIds')).toMatchObject({
      before: '本地助理',
      after: '已停用的模型',
    });
    expect(changes.some((x) => x.label === '每日 token 上限 · 已停用的模型')).toBe(true);
    const details = auditDetails(json, names);
    expect(details).toContain('本地助理');
    expect(details).not.toContain(id);
    expect(details).not.toContain('removed/model');
    const legacy = JSON.stringify({
      before: { Policy: { DailyTokenLimits: { [id]: 100 } } },
      after: { Policy: { DailyTokenLimits: null } },
    });
    expect(auditChanges(legacy, names)).toEqual([
      expect.objectContaining({ label: '每日 token 上限 · 本地助理', after: '繼承設定' }),
    ]);
    expect(auditDetails(legacy, names)).not.toContain(id);
  });
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
