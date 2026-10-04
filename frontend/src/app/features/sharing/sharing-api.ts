import { inject, Injectable } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type { ReadonlyShare, SharedContent } from '../../core/api/types';
@Injectable({ providedIn: 'root' })
export class SharingApi {
  private readonly http = inject(ApiTransport);
  list = (sent = false) => this.http.json<ReadonlyShare[]>(`/shares?sent=${sent}`);
  get = (id: string) => this.http.json<SharedContent>(`/shares/${id}`);
  create = (
    kind: string,
    sourceId: string,
    recipientIds: string[],
    hours: number,
    includeAttachments: boolean,
    artifactVersion: number | null,
  ) =>
    this.http.json<ReadonlyShare>('/shares', 'POST', {
      kind,
      sourceId,
      recipientIds,
      hours,
      includeAttachments,
      artifactVersion,
    });
  revoke = (id: string) => this.http.json<void>(`/shares/${id}`, 'DELETE');
}
