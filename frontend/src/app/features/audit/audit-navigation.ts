import type { AuditDto } from '../../core/api/schema';

export const AUDIT_CATEGORIES = [
  { value: '', label: '全部活動' },
  { value: 'authentication', label: '登入與身分' },
  { value: 'administration', label: '管理異動' },
  { value: 'access', label: '查閱與匯出' },
  { value: 'activity', label: '功能操作' },
];

/** Use the event time, not today's default range, for investigations of older activity. */
export function auditLogQuery(entry: AuditDto): Record<string, string> | null {
  const time = Date.parse(entry.at);
  const traceId = /^[a-f\d]{32}$/i.test(entry.traceId || '') ? entry.traceId! : '';
  const issueCode = /^NX-[a-f\d]{32}$/i.test(entry.issueCode || '') ? entry.issueCode! : '';
  if (!Number.isFinite(time) || (!traceId && !issueCode)) return null;
  return {
    ...(traceId ? { traceId } : { issueCode }),
    from: new Date(time - 5 * 60_000).toISOString(),
    to: new Date(time + 5 * 60_000).toISOString(),
  };
}
