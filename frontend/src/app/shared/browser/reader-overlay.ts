import { Injectable, inject, signal } from '@angular/core';
import { routeOf } from '@core/api/catalog';
import { AuthService } from '@core/auth/auth-service';

export interface ReaderTarget {
  id: string;
  attachment: boolean;
  page?: number | null;
  shareId?: string | null;
}

/**
 * 閱讀器網址的路徑段：分享的檔案、檔案庫的附件或知識庫的文件，各自是目錄裡的一個閱讀器。
 * 給 `createUrlTree` 用；字串網址用 {@link readerUrl}。
 */
export function readerCommands(target: Pick<ReaderTarget, 'id' | 'attachment' | 'shareId'>) {
  return target.shareId
    ? [routeOf('sharing.reader'), target.shareId, target.id]
    : [routeOf(target.attachment ? 'files.reader' : 'knowledge.reader'), target.id];
}

/** 閱讀器的完整網址（新分頁、外部開啟），ID 逐段編碼；目錄的基底網址本身不編碼。 */
export function readerUrl(target: Pick<ReaderTarget, 'id' | 'attachment' | 'shareId'>, page = 1) {
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
