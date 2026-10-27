import { catalogFeature } from '@core/api/catalog';
import { CATALOG_SCOPES } from '@core/api/feature-catalog';
import { publicErrorHints } from '@core/errors/public-error-catalog';
import { formatNumber } from '@shared/browser/format';

/** 成功的稽核結果；其餘都是錯誤代碼（ADR 0023）。 */
export const AUDIT_COMPLETED = 'completed';
/** 授權或登入不足而被擋下的代碼，標成「已拒絕」；其他錯誤代碼標成「失敗」。 */
const DENIED = new Set(['feature_forbidden', 'access_denied', 'authentication_required']);

/** 列表與詳情共用的結果標示；`reason` 是錯誤代碼的公開說明，查不到時顯示代碼本身。 */
export function auditOutcome(result: string) {
  if (result === AUDIT_COMPLETED) return { result: '完成', rejected: false, reason: '' };
  return {
    result: DENIED.has(result) ? '已拒絕' : '失敗',
    rejected: true,
    reason: publicErrorHints[result] ?? result,
  };
}

const authenticationMethods: Record<string, string> = {
  local: '本地帳號',
  ad: 'AD 驗證',
  windows: 'Windows 整合驗證',
  test: '測試身分',
  unknown: '未識別',
};
/** 詳情裡值得單獨列出的欄位：登入嘗試的帳號與方式。 */
export function auditContext(json: string | null | undefined) {
  try {
    const details = JSON.parse(json || '{}');
    return [
      { label: '嘗試／登入帳號', value: details.account },
      { label: '登入方式', value: authenticationMethods[details.authentication] },
    ].filter(
      (field): field is { label: string; value: string } =>
        typeof field.value === 'string' && !!field.value,
    );
  } catch {
    return [];
  }
}
export interface AuditChange {
  key: string;
  label: string;
  before: string;
  after: string;
  featureIds?: { before: string[]; after: string[] };
}
const labels: Record<string, string> = {
  name: '名稱',
  displayName: '姓名',
  account: '帳號',
  attachmentLimitBytes: '個人原檔容量上限（bytes）',
  securityVersion: '登入撤銷版本',
  deletedAt: '移除時間',
  'authentication.adEnabled': 'AD 驗證',
  'authentication.localEnabled': '本地密碼驗證',
  'authentication.adAccount': 'AD 帳號',
  'authentication.localAccount': '本地帳號',
  'authentication.hasLocalPassword': '已設定本地密碼',
  enabled: '啟用狀態',
  roleIds: '角色',
  groupIds: '功能群組',
  featureIds: '功能',
  scopes: '資料範圍',
  sortOrder: '顯示順序',
  parentId: '上層分群',
  kind: '種類',
  featureId: '頁面',
  openInNewTab: '開新分頁',
  icon: '圖示',
  audienceGroupIds: '可見群組',
  'policy.allowedModelIds': '可用模型',
  'policy.dailyRequestLimit': '舊制每日生成次數上限',
  allowedModelIds: '可用模型',
  'policy.dailyTokenLimits': '各模型每日 token 上限',
  dailyTokenLimits: '各模型每日 token 上限',
  'policy.storedAttachmentLimitBytes': '附件空間上限（bytes）',
};
/** 各模型每日 token 上限；舊紀錄的鍵是 PascalCase，所以不分大小寫。 */
const TOKEN_LIMITS = /^(?:policy\.)?dailyTokenLimits$/i;
const TOKEN_LIMIT = /^(?:policy\.)?dailyTokenLimits\./i;
const MODEL_ID = /^(?:modelId|defaultModelId|providerModelId)$/i;
const MODEL_IDS = /^allowedModelIds$/i;
function flatten(value: unknown, prefix = ''): Record<string, unknown> {
  // 上限整組清除（null）時不展開成一列，改由各模型的鍵顯示「繼承設定」。
  if (value == null && TOKEN_LIMITS.test(prefix)) return {};
  if (!value || typeof value !== 'object' || Array.isArray(value)) return { [prefix]: value };
  return Object.fromEntries(
    Object.entries(value).flatMap(([key, item]) =>
      Object.entries(flatten(item, prefix ? prefix + '.' + key : key)),
    ),
  );
}
const display = (value: unknown) =>
  value == null
    ? '未設定'
    : typeof value === 'boolean'
      ? value
        ? '啟用'
        : '停用'
      : Array.isArray(value)
        ? value.join('、') || '無'
        : String(value);
const resolveModel = (id: unknown, names: Readonly<Record<string, string>>) =>
  names[String(id)] || '已停用的模型';
const tokenLimit = (value: unknown) =>
  value == null ? '繼承設定' : formatNumber(Number(value)) + ' tokens';
export function auditChanges(
  json: string | null | undefined,
  modelNames: Readonly<Record<string, string>> = {},
): AuditChange[] {
  try {
    const data = JSON.parse(json || '{}');
    if (!Object.hasOwn(data, 'after')) return [];
    const before = data.before == null ? {} : flatten(data.before),
      after = data.after == null ? {} : flatten(data.after);
    return [...new Set([...Object.keys(before), ...Object.keys(after)])]
      .filter((key) => JSON.stringify(before[key]) !== JSON.stringify(after[key]))
      .map((key) => {
        const tokenPrefix = TOKEN_LIMIT.exec(key)?.[0];
        const modelId = tokenPrefix ? key.slice(tokenPrefix.length) : null;
        return {
          key,
          label: modelId
            ? '每日 token 上限 · ' + resolveModel(modelId, modelNames)
            : labels[key] || key,
          before: modelId
            ? tokenLimit(before[key])
            : displayModelValue(key, before[key], modelNames),
          after: modelId ? tokenLimit(after[key]) : displayModelValue(key, after[key], modelNames),
          ...(key === 'featureIds'
            ? {
                featureIds: {
                  before: Array.isArray(before[key]) ? (before[key] as unknown[]).map(String) : [],
                  after: Array.isArray(after[key]) ? (after[key] as unknown[]).map(String) : [],
                },
              }
            : {}),
        };
      });
  } catch {
    return [];
  }
}
/**
 * 群組授權的資料範圍快照是 `[{ featureId, scope, managerScope }]`（稽核遮罩只留白名單欄位，所以不是以功能為鍵的物件）；
 * 舊快照沒有 `managerScope`。
 */
function displayScopes(value: unknown[]) {
  const name = (scope: string) => CATALOG_SCOPES.find((x) => x.id === scope)?.name ?? scope;
  return (
    value
      .map((item) => {
        const { featureId, scope, managerScope } = (item ?? {}) as {
          featureId?: string;
          scope?: string | null;
          managerScope?: string | null;
        };
        const feature = catalogFeature(String(featureId))?.name ?? String(featureId);
        const parts = [
          scope == null ? '' : name(scope),
          managerScope == null ? '' : `主管另可看${name(managerScope)}`,
        ].filter(Boolean);
        return `${feature}：${parts.join('，')}`;
      })
      .join('、') || '無'
  );
}
function displayModelValue(key: string, value: unknown, names: Readonly<Record<string, string>>) {
  if (key === 'scopes' && Array.isArray(value)) return displayScopes(value);
  const field = key.slice(key.lastIndexOf('.') + 1);
  if (MODEL_IDS.test(field) && Array.isArray(value))
    return value.map((id) => resolveModel(id, names)).join('、') || '無';
  if (MODEL_ID.test(field) && value != null) return resolveModel(value, names);
  return display(value);
}
export function auditDetails(
  json: string | null | undefined,
  names: Readonly<Record<string, string>>,
): string {
  try {
    const present = (value: unknown, field = ''): unknown => {
      if (MODEL_ID.test(field)) return value == null ? value : resolveModel(value, names);
      if (MODEL_IDS.test(field) && Array.isArray(value))
        return value.map((id) => resolveModel(id, names));
      if (/^dailyTokenLimits$/i.test(field) && value && typeof value === 'object')
        return Object.entries(value).map(([id, limit]) => ({
          model: resolveModel(id, names),
          limit,
        }));
      if (Array.isArray(value)) return value.map((item) => present(item));
      if (value && typeof value === 'object')
        return Object.fromEntries(
          Object.entries(value).map(([name, item]) => [name, present(item, name)]),
        );
      return value;
    };
    return JSON.stringify(present(JSON.parse(json || '{}')), null, 2);
  } catch {
    return '無法解析此稽核內容。';
  }
}
export function auditResource(json: string | null | undefined): string {
  try {
    const value = JSON.parse(json || '{}');
    return String(value.resourceKey || value.conversationId || value.userId || '');
  } catch {
    return '';
  }
}
