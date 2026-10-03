import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NexusApi } from '../api/nexus-api';
import type { AuthSession } from '../api/types';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(NexusApi);
  private readonly router = inject(Router);
  readonly session = signal<AuthSession | null>(null);
  async load(): Promise<AuthSession> {
    const session = await this.api.authSession();
    this.session.set(session);
    return session;
  }
  async requireLogin(): Promise<boolean> {
    const session = await this.load();
    if (session.mode !== 'Ldap' || session.authenticated) return true;
    await this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
    return false;
  }
  async login(account: string, password: string) {
    this.session.set(await this.api.login(account, password));
  }
  async logout() {
    await this.api.logout();
    this.session.set(null);
    await this.router.navigate(['/login']);
  }
}
