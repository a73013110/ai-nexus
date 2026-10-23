import { computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { DirectoryUserDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { CollaborationApi } from './collaboration-api';

const MIN_QUERY_LENGTH = 2;

/**
 * 人員選擇器的查詢：輸入暫停後才搜尋目錄，從兩個字元起算。
 * 結果會排除 `chosen` 已列出的人，以及已登入的使用者（分享給自己沒有意義；`includeSelf` 用於管理員替自己分派，例如組織單位）。
 * `loading` 涵蓋防抖等待與請求進行中：結果為空且 `loading()` 時顯示「正在搜尋…」，不是「沒有符合的帳號」。
 * 需要 `ViewScope`。
 */
export function directorySearch(chosen: () => string[], options: { includeSelf?: boolean } = {}) {
  const api = inject(CollaborationApi);
  const scope = inject(ViewScope);
  const session = inject(WorkspaceSession);
  const text = signal('');
  const query = signal('');
  const read = apiResource({
    params: () => (query().length >= MIN_QUERY_LENGTH ? query() : undefined),
    loader: (value) => api.directory(value),
  });
  const results = computed<DirectoryUserDto[]>(() => {
    if (text().trim() !== query()) return [];
    const excluded = new Set([...(options.includeSelf ? [] : [session.me()?.id]), ...chosen()]);
    return (read.value() ?? []).filter((item) => !excluded.has(item.id));
  });
  const searchable = computed(() => text().trim().length >= MIN_QUERY_LENGTH);
  return {
    text: text.asReadonly(),
    /** 輸入已達查詢字數；結果區以此決定是否顯示，門檻只在這裡定義。 */
    searchable,
    results,
    /**
     * 搜尋還沒有答案：輸入已達門檻，且還在防抖等待（輸入與已送出的查詢不同）或請求進行中。
     * 請求失敗即結束，畫面改顯示 `error`，不會一直停在「正在搜尋…」。
     */
    loading: computed(() => searchable() && (text().trim() !== query() || read.refreshing())),
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
