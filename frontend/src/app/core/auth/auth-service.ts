import { DestroyRef, effect, Injectable, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { WorkspaceApi } from '../api/workspace-api';
import type { AuthSessionDto } from '../api/schema';
import { ApiClient } from '../api/api-client';
import { ViewMorph } from '../layout/view-morph';
import { WORKSPACE_HOME } from '../layout/workspace-home';
import { routeOf } from '../api/catalog';

const RECENT_SESSION_MS = 5000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(WorkspaceApi);
  private readonly router = inject(Router);
  private readonly morph = inject(ViewMorph);
  readonly session = signal<AuthSessionDto | null>(null);
  readonly generation = signal(0);
  private pending: Promise<AuthSessionDto> | null = null;
  private confirmedAt = 0;
  private readonly channel =
    typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('ai-nexus-identity');
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
  /** 回傳的檢查結果會維持為 true，直到帳號改變；遲到的回應會先檢查它。 */
  guard() {
    const generation = this.generation();
    return () => generation === this.generation();
  }
  /** 路由守衛剛確認過工作階段；緊接著載入的頁面會直接沿用它。 */
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
  /** 以帳號提供的另一個身分執行；頁面會為它重新載入資料。 */
  async selectIdentity(identity: string) {
    this.session.set(await this.api.selectIdentity(identity));
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
  }
  async logout() {
    await this.api.logout();
    this.session.set(null);
    // 工作區會收合進登出時所選的頭像（motion.scss，`sign-out`）。
    // 新的世代要等它離開後才開始，這樣它仍會顯示是誰登出，
    // 而且路上不會為已登出的帳號重新載入頁面。
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
    this.replaceIdentity(await this.api.testIdentity(userId, reason), WORKSPACE_HOME);
  }
  async endTestIdentity() {
    await this.load(); // Refresh CSRF if the server has already restored an expired test.
    // 測試身分從使用者頁開始，結束後回到那裡。
    this.replaceIdentity(await this.api.endTestIdentity(), routeOf('identity.users'));
  }
  private replaceIdentity(session: AuthSessionDto, url: string) {
    this.session.set(session);
    this.generation.update((value) => value + 1);
    this.channel?.postMessage('changed');
    // 罕見的身分轉換會一起清掉所有路由、提供者與進行中的檢視。
    window.location.assign(url);
  }
}
