import { describe, expect, it } from 'vitest';
import {
  auditChanges,
  auditContext,
  auditDetails,
  auditOutcome,
  auditResource,
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
    expect(changes.find((item2) => item2.key === 'allowedModelIds')).toMatchObject({
      before: '本地助理',
      after: '已停用的模型',
    });
    expect(changes.some((item) => item.label === '每日 token 上限 · 已停用的模型')).toBe(true);
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
  it('shows data scope changes with feature and scope names', () => {
    const changes = auditChanges(
      JSON.stringify({
        before: { scopes: [{ featureId: 'audit', scope: 'unit' }] },
        after: { scopes: [{ featureId: 'audit', scope: 'unit-tree' }] },
      }),
    );
    expect(changes).toEqual([
      expect.objectContaining({
        label: '資料範圍',
        before: '活動稽核：所屬單位',
        after: '活動稽核：所屬單位及下層',
      }),
    ]);
  });
  it('shows the manager scope next to the member scope', () => {
    const changes = auditChanges(
      JSON.stringify({
        before: { scopes: [{ featureId: 'attendance.records', scope: 'own', managerScope: null }] },
        after: {
          scopes: [{ featureId: 'attendance.records', scope: 'own', managerScope: 'managed-tree' }],
        },
      }),
    );
    expect(changes).toEqual([
      expect.objectContaining({
        before: '出勤紀錄：本人',
        after: '出勤紀錄：本人，主管另可看所管單位及下層',
      }),
    ]);
  });
  it('marks every error code as a failure and names denials and known reasons', () => {
    expect(auditOutcome('completed')).toEqual({ result: '完成', rejected: false, reason: '' });
    expect(auditOutcome('feature_forbidden')).toMatchObject({ result: '已拒絕', rejected: true });
    expect(auditOutcome('invalid_credentials')).toEqual({
      result: '失敗',
      rejected: true,
      reason: '帳號、密碼或登入方式不正確，或帳號暫時無法登入。',
    });
    // 沒有公開說明的代碼原樣顯示，仍算失敗。
    expect(auditOutcome('executor_lost')).toEqual({
      result: '失敗',
      rejected: true,
      reason: 'executor_lost',
    });
  });
  it('lists the attempted account and sign-in method', () => {
    expect(auditContext('{"account":"user01","authentication":"ad"}')).toEqual([
      { label: '嘗試／登入帳號', value: 'user01' },
      { label: '登入方式', value: 'AD 驗證' },
    ]);
    expect(auditContext('{')).toEqual([]);
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
      expect(changes.find((item3) => item3.key.endsWith(modelId))).toMatchObject({
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
