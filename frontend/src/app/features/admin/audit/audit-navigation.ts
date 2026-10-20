import type { AuditDto } from '@core/api/schema';
import { ISSUE_CODE, TRACE_ID } from '@core/errors/safe-errors';

/** 調查較早的活動時，使用事件時間，而非今天的預設範圍。 */
export function auditLogQuery(entry: AuditDto): Record<string, string> | null {
  const time = Date.parse(entry.at);
  const traceId = TRACE_ID.test(entry.traceId || '') ? entry.traceId! : '';
  const issueCode = ISSUE_CODE.test(entry.issueCode || '') ? entry.issueCode! : '';
  if (!Number.isFinite(time) || (!traceId && !issueCode)) return null;
  return {
    ...(traceId ? { traceId } : { issueCode }),
    from: new Date(time - 5 * 60_000).toISOString(),
    to: new Date(time + 5 * 60_000).toISOString(),
  };
}
