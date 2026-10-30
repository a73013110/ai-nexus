import { Injectable, inject, signal } from '@angular/core';
import { apiHref } from '@core/api/api-client';
import { routeOf } from '@core/api/catalog';
import { AuthService } from '@core/auth/auth-service';

/**
 * 其他系統的檔案（例如公文系統的附件）：後端發的短期憑證，內容由 `/api/v1/remote-files/content` 代為取回，
 * 前端看不到原系統的網址。只能預覽 PDF 與圖片，沒有擷取文字。
 */
export interface RemoteReaderFile {
  ticket: string;
  fileName: string;
  contentType: string;
}

/**
 * 由容器授權的唯讀檔案：分享的快照，或管理員稽核檢視中的對話。授權來自容器對檔案的引用，
 * 不是讀者對原始附件的存取權，所以預覽、原檔與 HTML 沙箱都走容器自己的路由（三者形狀相同）。
 * 新增一種容器只要在 {@link SCOPED_FILE_ROUTES} 加一列，閱讀器與檔案清單不必改。
 */
export interface FileScope {
  kind: 'share' | 'conversation';
  id: string;
}

export interface ReaderTarget {
  id: string;
  attachment: boolean;
  page?: number | null;
  scope?: FileScope | null;
  remote?: RemoteReaderFile | null;
}

export type ScopedFilePart = 'content' | 'preview' | 'render';

const SCOPED_FILE_ROUTES = {
  share: {
    content: '/api/v1/shares/{id}/files/{file}',
    preview: '/api/v1/shares/{id}/files/{file}/preview',
    render: '/api/v1/shares/{id}/files/{file}/render',
  },
  conversation: {
    content: '/api/v1/admin/conversations/{id}/files/{file}',
    preview: '/api/v1/admin/conversations/{id}/files/{file}/preview',
    render: '/api/v1/admin/conversations/{id}/files/{file}/render',
  },
} as const satisfies Record<FileScope['kind'], Record<ScopedFilePart, string>>;

/** 容器內檔案的 API 路徑與路徑參數，給 `ApiClient` 與 {@link scopedFileHref} 共用。 */
export function scopedFileRoute<T extends ScopedFilePart>(scope: FileScope, file: string, part: T) {
  return {
    path: SCOPED_FILE_ROUTES[scope.kind][part] as (typeof SCOPED_FILE_ROUTES)[FileScope['kind']][T],
    params: { path: { id: scope.id, file } },
  };
}

/** 容器內檔案的網址（縮圖、下載、HTML 沙箱）。 */
export function scopedFileHref(scope: FileScope, file: string, part: ScopedFilePart = 'content') {
  const route = scopedFileRoute(scope, file, part);
  return apiHref(route.path, route.params);
}

/**
 * 沒有閱讀器網址的檔案（遠端檔案、稽核檢視中的對話附件）直接以原檔當新分頁的網址；
 * 有閱讀器網址時回傳 null。
 */
export function originalOnlyHref(target: Pick<ReaderTarget, 'id' | 'scope' | 'remote'>) {
  if (target.remote) return remoteFileContent(target.remote.ticket);
  if (target.scope?.kind === 'conversation') return scopedFileHref(target.scope, target.id);
  return null;
}

/** 遠端檔案的內容網址；憑證放查詢字串（太長，放不進 IIS 的路徑段）。 */
export function remoteFileContent(ticket: string, download = false) {
  return apiHref('/api/v1/remote-files/content', {
    query: download ? { ticket, download: true } : { ticket },
  });
}

/**
 * 閱讀器網址的路徑段：分享的檔案、檔案庫的附件或知識庫的文件，各自是目錄裡的一個閱讀器。
 * 給 `createUrlTree` 用；字串網址用 {@link readerUrl}。
 */
export function readerCommands(target: Pick<ReaderTarget, 'id' | 'attachment' | 'scope'>) {
  return target.scope?.kind === 'share'
    ? [routeOf('sharing.reader'), target.scope.id, target.id]
    : [routeOf(target.attachment ? 'files.reader' : 'knowledge.reader'), target.id];
}

/** 閱讀器的完整網址（新分頁、外部開啟），ID 逐段編碼；目錄的基底網址本身不編碼。 */
export function readerUrl(target: Pick<ReaderTarget, 'id' | 'attachment' | 'scope'>, page = 1) {
  const [base, ...ids] = readerCommands(target);
  return `${[base, ...ids.map((id) => encodeURIComponent(id!))].join('/')}?page=${page}`;
}

/** 保留來源端的掛載，包括聊天草稿、捲動位置與進行中的生成。 */
@Injectable({ providedIn: 'root' })
export class ReaderOverlay {
  readonly target = signal<ReaderTarget | null>(null);
  readonly auth = inject(AuthService);
  open(target: ReaderTarget) {
    this.target.set(target);
  }
  close() {
    this.target.set(null);
  }
}
