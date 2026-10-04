import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from './auth-service';
import { NexusApi } from '../api/nexus-api';
import type { Me } from '../api/types';
import { UserSettingsService } from '../preferences/user-settings';

/** Account context shared by feature pages; it never loads conversation history. */
@Injectable({ providedIn: 'root' })
export class WorkspaceSession {
  readonly auth = inject(AuthService);
  readonly me = signal<Me | null>(null);
  private readonly api = inject(NexusApi);
  readonly settings = inject(UserSettingsService);
  private generation = -1;
  constructor() {
    effect(() => {
      const generation = this.auth.generation();
      if (generation !== this.generation) untracked(() => this.me.set(null));
    });
  }
  adopt(me: Me) {
    this.generation = this.auth.generation();
    this.me.set(me);
  }
  async load(refresh = false) {
    if (!refresh && this.me() && this.generation === this.auth.generation()) return this.me();
    const generation = this.auth.generation();
    if (!(await this.auth.requireLogin())) return null;
    const me = await this.api.me();
    if (generation !== this.auth.generation()) return null;
    this.adopt(me);
    await this.settings.load(me.id);
    return me;
  }
  has(id: string) {
    return this.me()?.access.features?.some((x) => x.id === id) ?? false;
  }
}
