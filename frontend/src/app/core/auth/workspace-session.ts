import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from './auth-service';
import { NexusApi } from '../api/nexus-api';
import type { MeDto } from '../api/schema';
import { UserSettingsService } from '../preferences/user-settings';
import { FEATURE_NAMES } from '../feature-names';

/** Account context shared by feature pages; it never loads conversation history. */
@Injectable({ providedIn: 'root' })
export class WorkspaceSession {
  readonly auth = inject(AuthService);
  readonly me = signal<MeDto | null>(null);
  private readonly api = inject(NexusApi);
  readonly settings = inject(UserSettingsService);
  private generation = -1;
  constructor() {
    effect(() => {
      const generation = this.auth.generation();
      if (generation !== this.generation) untracked(() => this.me.set(null));
    });
  }
  adopt(me: MeDto) {
    this.generation = this.auth.generation();
    this.me.set(me);
  }
  async load(refresh = false) {
    if (!refresh && this.me() && this.generation === this.auth.generation()) return this.me();
    const generation = this.auth.generation();
    if (!(await this.auth.requireLogin())) return null;
    const request = this.api.me();
    const [me] = await Promise.all([
      request,
      this.settings.load(
        request.then((value) => value.id),
        refresh,
      ),
    ]);
    if (generation !== this.auth.generation()) return null;
    this.adopt(me);
    return me;
  }
  has(id: string) {
    return this.me()?.access.features?.some((x) => x.id === id) ?? false;
  }
  featureName(id: string) {
    return this.me()?.access.features?.find((x) => x.id === id)?.name || FEATURE_NAMES[id] || id;
  }
}
