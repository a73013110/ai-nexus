import { inject, Injectable } from '@angular/core';
import { ApiTransport } from '../../../core/api/api-transport';
import type { components } from '../../../core/api/schema';
import type { TableSortDirection } from '../../../shared/ui/data-table';

export type LogEntry = components['schemas']['DiagnosticSummary'];
export type LogHealth = components['schemas']['DiagnosticHealthDto'];
export type LogDetail = components['schemas']['DiagnosticDetail'];
export type LogPage = components['schemas']['DiagnosticPage'];
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
@Injectable({ providedIn: 'root' })
export class SystemLogsApi {
  private readonly transport = inject(ApiTransport);
  private path(filter: LogFilter, cursor?: string | null) {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(filter)) if (value) query.set(key, String(value));
    query.set('take', String(filter.take ?? 50));
    if (cursor) query.set('cursor', cursor);
    return '/admin/logs?' + query;
  }
  list(filter: LogFilter, cursor?: string | null, signal?: AbortSignal) {
    return this.transport.json<LogPage>(
      this.path(filter, cursor),
      'GET',
      undefined,
      undefined,
      signal,
    );
  }
  detail(id: string, signal?: AbortSignal) {
    return this.transport.json<LogDetail>(
      '/admin/logs/' + encodeURIComponent(id),
      'GET',
      undefined,
      undefined,
      signal,
    );
  }
  health() {
    return this.transport.json<LogHealth>('/admin/logs/health');
  }
  export(filter: LogFilter) {
    return this.transport.response(
      this.path(filter).replace('/admin/logs?', '/admin/logs/export?'),
    );
  }
}
