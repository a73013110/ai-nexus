const actions: Record<string, string> = {
  'admin.bootstrap': '初始化管理員',
  'admin.user_roles': '調整使用者角色',
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
};
export const auditAction = (action: string) => actions[action] || action;
const outcomes: Record<string, string> = {
  saved: '已儲存',
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
export const auditResult = (result: string | null | undefined) =>
  result ? outcomes[result] || result : '未記錄';
export const auditRejected = (result: string | null | undefined) =>
  !!result && !Object.hasOwn(outcomes, result);
export interface AuditChange {
  key: string;
  label: string;
  before: string;
  after: string;
}
const labels: Record<string, string> = {
  name: '名稱',
  enabled: '啟用狀態',
  roleIds: '角色',
  groupIds: '功能群組',
  featureIds: '功能',
  sortOrder: '顯示順序',
  'policy.allowedModelIds': '可用模型',
  'policy.dailyRequestLimit': '每日生成上限',
  'policy.storedAttachmentLimitBytes': '附件空間上限（bytes）',
};
function flatten(value: unknown, prefix = ''): Record<string, unknown> {
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
export function auditChanges(json: string | null | undefined): AuditChange[] {
  try {
    const data = JSON.parse(json || '{}');
    if (!Object.hasOwn(data, 'after')) return [];
    const before = data.before == null ? {} : flatten(data.before),
      after = data.after == null ? {} : flatten(data.after);
    return [...new Set([...Object.keys(before), ...Object.keys(after)])]
      .filter((key) => JSON.stringify(before[key]) !== JSON.stringify(after[key]))
      .map((key) => ({
        key,
        label: labels[key] || key,
        before: display(before[key]),
        after: display(after[key]),
      }));
  } catch {
    return [];
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
