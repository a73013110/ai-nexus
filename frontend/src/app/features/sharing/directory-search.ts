import { computed, inject, signal } from '@angular/core';
import { apiResource } from '../../core/api/api-resource';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type { DirectoryUserDto } from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { ResourceApi } from './resource-api';

/**
 * People picker lookup: the directory is searched once typing pauses, from two characters on.
 * Results leave out the signed-in user and anyone `chosen` already lists. Needs a `ViewScope`.
 */
export function directorySearch(chosen: () => string[]) {
  const api = inject(ResourceApi);
  const scope = inject(ViewScope);
  const session = inject(WorkspaceSession);
  const text = signal('');
  const query = signal('');
  const read = apiResource({
    params: () => (query().length >= 2 ? query() : undefined),
    loader: (value) => api.directory(value),
  });
  const results = computed<DirectoryUserDto[]>(() => {
    if (text().trim() !== query()) return [];
    const excluded = new Set([session.me()?.id, ...chosen()]);
    return (read.value() ?? []).filter((x) => !excluded.has(x.id));
  });
  return {
    text: text.asReadonly(),
    results,
    error: read.error,
    find(value: string) {
      text.set(value);
      scope.later(() => query.set(value.trim()), 250, 'directory');
    },
    clear() {
      scope.cancel('directory');
      text.set('');
      query.set('');
    },
  };
}
