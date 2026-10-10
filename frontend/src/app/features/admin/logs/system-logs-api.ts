import { inject, Injectable } from '@angular/core';
import { ApiClient } from '../../../core/api/api-client';
import type { LogLevel } from '../../../core/api/schema';
import type { TableSortDirection } from '../../../shared/ui/data-table';

/** The log page's filter form; blank fields are left out of the query. */
export interface LogFilter {
  from: string;
  to: string;
  level: string;
  category: string;
  eventName: string;
  issueCode: string;
  traceId: string;
  jobId: string;
  runId: string;
  errorCode: string;
  instance: string;
  text: string;
  take?: number;
  sortDirection?: TableSortDirection;
}
const optional = (value: string) => value || undefined;
function query(filter: LogFilter, cursor?: string | null) {
  return {
    from: optional(filter.from),
    to: optional(filter.to),
    level: optional(filter.level) as LogLevel | undefined,
    category: optional(filter.category),
    eventName: optional(filter.eventName),
    issueCode: optional(filter.issueCode),
    traceId: optional(filter.traceId),
    jobId: optional(filter.jobId),
    runId: optional(filter.runId),
    errorCode: optional(filter.errorCode),
    instance: optional(filter.instance),
    text: optional(filter.text),
    sortDirection: filter.sortDirection,
    take: filter.take ?? 50,
    cursor: cursor ?? undefined,
  };
}

@Injectable({ providedIn: 'root' })
export class SystemLogsApi {
  private readonly api = inject(ApiClient);
  list = (filter: LogFilter, cursor?: string | null, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/logs', { query: query(filter, cursor), signal });
  detail = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/logs/{id}', { path: { id }, signal });
  health = () => this.api.get('/api/v1/admin/logs/health');
  export = (filter: LogFilter) =>
    this.api.open('/api/v1/admin/logs/export', { query: query(filter) });
}
