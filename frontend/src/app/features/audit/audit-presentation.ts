const actions: Record<string, string> = {
  'conversation.created': '建立對話',
  'conversation.renamed': '重新命名對話',
  'conversation.deleted': '刪除對話',
  'conversation.organized': '整理對話',
  'conversation.imported': '匯入對話',
  'file.renamed': '重新命名檔案',
  'file.retained': '保留檔案',
  'share.created': '建立分享',
  'share.revoked': '撤銷分享',
  'repository.connected': '連結程式庫',
  'repository.disconnected': '中斷程式庫連結',
  'repository.review.created': '建立程式碼檢查',
  'web.search': '執行網路搜尋',
  'identity.login': '登入工作區',
  'identity.logout': '登出工作區',
  'identity.first_seen': '首次建立身分',
  'logs.query': '查詢系統日誌',
  'logs.detail': '查閱日誌詳情',
  'logs.export': '匯出系統日誌',
  'logs.health': '檢視日誌健康狀態',
  'billing.report.read': '檢視費用報表',
  'billing.report.export': '匯出費用報表',
  'dashboard.platform.read': '檢視平台用量',
  'admin.bootstrap': '初始化管理員',
  'admin.user_roles': '調整使用者角色',
  'admin.user_model_policy': '設定個人模型政策',
  'admin.user_storage': '調整使用者容量',
  'admin.user': '建立或調整使用者',
  'admin.user_delete': '移除使用者',
  'identity.test_start': '開始測試身分',
  'identity.test_end': '結束測試身分',
  'admin.role': '調整角色與群組',
  'admin.group': '調整群組、功能與模型政策',
  'admin.feature': '調整功能',
  'admin.user_usage_read': '檢視使用者用量',
  'admin.conversations_list': '檢視使用者對話清單',
  'admin.conversation_read': '檢視對話內容',
  'project.created': '建立專案',
  'project.updated': '調整專案',
  'project.deleted': '刪除專案',
  'project.template.created': '建立專案範本',
  'project.template.updated': '調整專案範本',
  'project.template.deleted': '刪除專案範本',
  'evaluation.deleted': '刪除評測題庫',
  'knowledge.created': '建立知識庫',
  'knowledge.updated': '調整知識庫',
  'knowledge.deleted': '刪除知識庫',
  'document.created': '加入文件',
  'document.reindexed': '重新索引文件',
  'document.deleted': '刪除文件',
  'run.recovered': '處理生成程序中斷',
  'billing.price.created': '新增價格版本',
};
export const auditAction = (action: string) => actions[action] || action;
const outcomes: Record<string, string> = {
  activated: '已啟用',
  cleared: '已清除',
  connected: '已連線',
  csv: '已匯出',
  metrics: '已檢視',
  reindexing: '重新索引中',
  snapshot: '已建立快照',
  saved: '已儲存',
  started: '已開始',
  restored: '已返回',
  read: '已檢視',
  completed: '已完成',
  success: '已完成',
  granted: '已授權',
  granted_once: '已初始化授權',
  created: '已建立',
  deleted: '已刪除',
  soft_deleted: '已移除',
  queued: '已排程',
  running: '處理中',
  cancelled: '已取消',
  revoked: '已撤銷',
  removed: '已移除',
  assigned: '已指派',
  'read-only': '已檢視',
  private: '已儲存私人快照',
};
export const auditResult = (result: string | null | undefined, action?: string) => {
  if (action === 'identity.login')
    return result === 'success' ? '登入成功' : result === 'failed' ? '登入未完成' : '未記錄';
  if (action === 'identity.logout' && result === 'completed') return '已登出';
  return result === 'failed'
    ? '未完成'
    : result === 'executor_lost'
      ? '執行程序中斷'
      : result
        ? outcomes[result] || result
        : '未記錄';
};
export const auditRejected = (result: string | null | undefined) =>
  !!result && !Object.hasOwn(outcomes, result);

export function auditContext(json: string | null | undefined) {
  try {
    const details = JSON.parse(json || '{}');
    const methods: Record<string, string> = {
      local: '本地帳號',
      ad: 'AD 驗證',
      windows: 'Windows 整合驗證',
      test: '測試身分',
      unknown: '未識別',
    };
    return [
      { label: '嘗試／登入帳號', value: details.account },
      { label: '登入方式', value: methods[details.authentication] },
      { label: '來源 IP', value: details.clientAddress },
      {
        label: '拒絕／失敗原因',
        value:
          typeof details.failureCode === 'string'
            ? (
                {
                  invalid_credentials: '帳號、密碼或登入方式不正確，或帳號暫時無法登入',
                  ad_invalid_credentials: 'AD 帳號或密碼不正確，或帳號無法登入',
                  login_method_disabled: '帳號已停用，或未允許此登入方式',
                  authentication_mode: '部署未支援所選登入方式',
                  service_unavailable: '登入服務暫時無法使用',
                } as Record<string, string>
              )[details.failureCode] || details.failureCode
            : null,
      },
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
  sortOrder: '顯示順序',
  'policy.allowedModelIds': '可用模型',
  'policy.dailyRequestLimit': '舊制每日生成次數上限',
  allowedModelIds: '可用模型',
  'policy.dailyTokenLimits': '各模型每日 token 上限',
  dailyTokenLimits: '各模型每日 token 上限',
  'policy.storedAttachmentLimitBytes': '附件空間上限（bytes）',
};
function flatten(value: unknown, prefix = ''): Record<string, unknown> {
  if (
    value == null &&
    ['dailytokenlimits', 'policy.dailytokenlimits'].includes(prefix.toLowerCase())
  )
    return {};
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
        const tokenPrefix = ['policy.dailyTokenLimits.', 'dailyTokenLimits.'].find((prefix) =>
          key.toLowerCase().startsWith(prefix.toLowerCase()),
        );
        const modelId = tokenPrefix ? key.slice(tokenPrefix.length) : null;
        const tokenValue = (value: unknown) =>
          value == null ? '繼承設定' : Number(value).toLocaleString('zh-TW') + ' tokens';
        return {
          key,
          label: modelId
            ? '每日 token 上限 · ' + resolveModel(modelId, modelNames)
            : labels[key] || key,
          before: modelId
            ? tokenValue(before[key])
            : displayModelValue(key, before[key], modelNames),
          after: modelId ? tokenValue(after[key]) : displayModelValue(key, after[key], modelNames),
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
function displayModelValue(key: string, value: unknown, names: Readonly<Record<string, string>>) {
  if (/(^|\.)allowedModelIds$/i.test(key) && Array.isArray(value))
    return value.map((id) => resolveModel(id, names)).join('、') || '無';
  if (/(^|\.)(modelId|defaultModelId|providerModelId)$/i.test(key) && value != null)
    return resolveModel(value, names);
  return display(value);
}
export function auditDetails(
  json: string | null | undefined,
  names: Readonly<Record<string, string>>,
): string {
  try {
    const present = (value: unknown, key = ''): unknown => {
      const field = key.toLowerCase();
      if (['modelid', 'defaultmodelid', 'providermodelid'].includes(field))
        return value == null ? value : resolveModel(value, names);
      if (field === 'allowedmodelids' && Array.isArray(value))
        return value.map((id) => resolveModel(id, names));
      if (field === 'dailytokenlimits' && value && typeof value === 'object')
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
