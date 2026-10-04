import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type { Job } from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class JobsApi {
  private readonly http = inject(ApiTransport);
  list = () => this.http.json<Job[]>('/jobs');
  get = (id: string, signal?: AbortSignal) =>
    this.http.json<Job>(`/jobs/${encodeURIComponent(id)}`, 'GET', undefined, undefined, signal);
  cancel = (id: string) => this.http.json<Job>(`/jobs/${encodeURIComponent(id)}/cancel`, 'POST');
  retry = (id: string) => this.http.json<Job>(`/jobs/${encodeURIComponent(id)}/retry`, 'POST');
}
