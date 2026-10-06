import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth-service';

export interface ReaderTarget {
  id: string;
  attachment: boolean;
  page?: number | null;
  shareId?: string | null;
}

/** Keeps the origin mounted, including chat drafts, scroll position and active generation. */
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
