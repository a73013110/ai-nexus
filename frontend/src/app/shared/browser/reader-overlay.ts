import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from '@core/auth/auth-service';

export interface ReaderTarget {
  id: string;
  attachment: boolean;
  page?: number | null;
  shareId?: string | null;
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
