import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { LogLevel } from '@core/api/schema';
import type { TableSortDirection } from '@shared/ui/data-table';

/** One read of the log query; blank fields are left out of the request. */
export interface LogFilter {
  from: string;
  to: string;
  levels: LogLevel[];
  category: string;
  eventName: string;
  issueCode: string;
  traceId: string;
  jobId: string;
  runId: string;
  operationId: string;
  errorCode: string;
  instance: string;
  text: string;
  take?: number;
  sortDirection?: TableSortDirection;
}
export const emptyLogFilter = (from: string, to: string): LogFilter => ({
  from,
  to,
  levels: [],
  category: '',
  eventName: '',
  issueCode: '',
  traceId: '',
  jobId: '',
  runId: '',
  operationId: '',
  errorCode: '',
  instance: '',
  text: '',
});
const optional = (value: string) => value || undefined;
function query(filter: LogFilter, cursor?: string | null) {
  return {
    from: optional(filter.from),
    to: optional(filter.to),
    levels: filter.levels.length ? filter.levels : undefined,
    category: optional(filter.category),
    eventName: optional(filter.eventName),
    issueCode: optional(filter.issueCode),
    traceId: optional(filter.traceId),
    jobId: optional(filter.jobId),
    runId: optional(filter.runId),
    operationId: optional(filter.operationId),
    errorCode: optional(filter.errorCode),
    instance: optional(filter.instance),
    text: optional(filter.text),
    sortDirection: filter.sortDirection,
    take: filter.take ?? 50,
    cursor: cursor ?? undefined,
  };
}

@Injectable({ providedIn: 'root' })
export class DiagnosticsApi {
  private readonly api = inject(ApiClient);
  list = (filter: LogFilter, cursor?: string | null, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/logs', { query: query(filter, cursor), signal });
  detail = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/logs/{id}', { path: { id }, signal });
  export = (filter: LogFilter) =>
    this.api.open('/api/v1/admin/logs/export', { query: query(filter) });
}
