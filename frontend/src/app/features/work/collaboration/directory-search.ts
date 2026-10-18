import { computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { DirectoryUserDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { CollaborationApi } from './collaboration-api';

/**
 * 人員選擇器的查詢：輸入暫停後才搜尋目錄，從兩個字元起算。
 * 結果會排除已登入的使用者與 `chosen` 已列出的人。需要 `ViewScope`。
 */
export function directorySearch(chosen: () => string[]) {
  const api = inject(CollaborationApi);
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
    return (read.value() ?? []).filter((item) => !excluded.has(item.id));
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
