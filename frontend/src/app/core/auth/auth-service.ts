import { DestroyRef, effect, Injectable, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { NexusApi } from '../api/nexus-api';
import type { AuthSessionDto } from '../api/schema';
import { ApiClient } from '../api/api-client';
import { ViewMorph } from '../layout/view-morph';

const RECENT_SESSION_MS = 5000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(NexusApi);
  private readonly router = inject(Router);
  private readonly morph = inject(ViewMorph);
  readonly session = signal<AuthSessionDto | null>(null);
  readonly generation = signal(0);
  private pending: Promise<AuthSessionDto> | null = null;
  private confirmedAt = 0;
  private readonly channel =
    typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('nexus-identity');
  constructor() {
    const transport = inject(ApiClient);
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
  async load(): Promise<AuthSessionDto> {
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
  /** The returned check stays true until the account changes; late responses test it first. */
  guard() {
    const generation = this.generation();
    return () => generation === this.generation();
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
  /** Acts as another identity the account offers; pages reload their data for it. */
  async selectIdentity(identity: string) {
    this.session.set(await this.api.selectIdentity(identity));
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
  }
  async logout() {
    await this.api.logout();
    this.session.set(null);
    // The workspace closes into the avatar the sign-out was chosen from (motion.scss, `sign-out`).
    // The new generation waits until it has left, so it still shows who signed out and its pages
    // do not reload for the signed-out account on the way.
    const avatar = document.querySelector('nx-account-menu .avatar')?.getBoundingClientRect();
    this.morph.next(
      'sign-out',
      avatar?.width
        ? { x: avatar.x + avatar.width / 2, y: avatar.y + avatar.height / 2 }
        : undefined,
    );
    await this.router.navigate(['/login']);
    this.morph.discard();
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
  }
  async testIdentity(userId: string, reason: string) {
    this.replaceIdentity(await this.api.testIdentity(userId, reason), '/dashboard');
  }
  async endTestIdentity() {
    await this.load(); // Refresh CSRF if the server has already restored an expired test.
    this.replaceIdentity(await this.api.endTestIdentity(), '/admin');
  }
  private replaceIdentity(session: AuthSessionDto, url: string) {
    this.session.set(session);
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
    // A rare identity transition clears every route/provider and in-flight view together.
    window.location.assign(url);
  }
}
