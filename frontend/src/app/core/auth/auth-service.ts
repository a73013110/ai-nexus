import { DestroyRef, effect, Injectable, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { NexusApi } from '../api/nexus-api';
import type { AuthSession } from '../api/types';
import { ApiTransport } from '../api/api-transport';

const RECENT_SESSION_MS = 5000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(NexusApi);
  private readonly router = inject(Router);
  readonly session = signal<AuthSession | null>(null);
  readonly generation = signal(0);
  private pending: Promise<AuthSession> | null = null;
  private confirmedAt = 0;
  private readonly channel =
    typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('nexus-identity');
  constructor() {
    const transport = inject(ApiTransport);
    if (this.channel) this.channel.onmessage = () => window.location.reload();
    inject(DestroyRef).onDestroy(() => this.channel?.close());
    effect(() => {
      if (transport.expired())
        untracked(() => {
          this.session.set(null);
          this.generation.update((value) => value + 1);
        });
    });
  }
  async load(): Promise<AuthSession> {
    if (this.pending) return this.pending;
    this.pending = this.api.authSession().then((session) => {
      const previous = this.session();
      if (
        previous?.authenticated &&
        (previous.account !== session.account ||
          previous.testing?.userId !== session.testing?.userId)
      )
        this.generation.update((value) => value + 1);
      this.session.set(session);
      this.confirmedAt = session.authenticated ? Date.now() : 0;
      return session;
    });
    try {
      return await this.pending;
    } finally {
      this.pending = null;
    }
  }
  /** The route guard has just confirmed the session; a page loading right after reuses it. */
  async requireLogin(): Promise<boolean> {
    if (this.session()?.authenticated && Date.now() - this.confirmedAt < RECENT_SESSION_MS)
      return true;
    const session = await this.load();
    if (session.authenticated) return true;
    await this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
    return false;
  }
  async login(account: string, password: string, method = 'ad') {
    this.session.set(await this.api.login(account, password, method));
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
  }
  async windowsLogin() {
    if (this.session()?.method === 'local' || this.session()?.testing) {
      this.session.set(await this.api.logout());
      this.generation.update((value) => value + 1);
      this.channel?.postMessage('changed');
    }
    this.session.set(await this.api.windowsLogin());
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
  }
  async logout() {
    await this.api.logout();
    this.session.set(null);
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
    await this.router.navigate(['/login']);
  }
  async testIdentity(userId: string, reason: string) {
    this.replaceIdentity(await this.api.testIdentity(userId, reason), '/dashboard');
  }
  async endTestIdentity() {
    await this.load(); // Refresh CSRF if the server has already restored an expired test.
    this.replaceIdentity(await this.api.endTestIdentity(), '/admin');
  }
  private replaceIdentity(session: AuthSession, url: string) {
    this.session.set(session);
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
    // A rare identity transition clears every route/provider and in-flight view together.
    window.location.assign(url);
  }
}
