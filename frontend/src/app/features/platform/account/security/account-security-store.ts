import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type {
  AccountSecurityChangeDto,
  AccountSecurityDto,
  LocalLoginRequest,
  ReauthenticationRequest,
} from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { ApiError, safeMessage } from '@core/errors/safe-errors';
import { ToastService } from '@shared/ui/toast-service';
import { AccountSecurityApi } from './account-security-api';

export type SecurityDialog = 'reauthenticate' | 'email' | 'local-login';

/** 信箱狀態的徽章文字與色調；AD 提供的信箱由目錄同步，視為已驗證。 */
export function emailBadge(
  security: Pick<AccountSecurityDto, 'email' | 'emailSource' | 'emailVerifiedAt'>,
) {
  if (!security.email) return { label: '尚未設定', tone: 'neutral' as const };
  if (security.emailSource === 'directory') return { label: 'AD 提供', tone: 'info' as const };
  return security.emailVerifiedAt
    ? { label: '已驗證', tone: 'success' as const }
    : { label: '未驗證', tone: 'warning' as const };
}

/**
 * 帳號分頁的自助維護狀態，由 `AccountTab` 提供、兩張卡片與三個對話框共用。敏感動作先檢查步升：
 * 需要時先開「確認身分」，成功後自動接續原本的動作；伺服器回 `reauthentication_required`（步升已過期）時也一樣。
 * 會換發 session 的回應帶新的 CSRF token，經 `AuthService.renewed` 換上，其他分頁同步重讀。
 */
@Injectable()
export class AccountSecurityStore {
  private readonly api = inject(AccountSecurityApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly read = apiResource({ loader: (_, signal) => this.api.get(signal) });
  readonly security = this.read.value;
  readonly loading = this.read.loading;
  readonly error = this.read.error;
  readonly dialog = signal<SecurityDialog | null>(null);
  /** 停用本地登入送出中（確認身分之後才開始算）。 */
  readonly disabling = signal(false);
  /** 唯讀（測試身分）時不顯示任何變更按鈕。 */
  readonly editable = computed(() => !!this.security() && !this.security()!.readOnly);
  private after: (() => void) | null = null;

  reload() {
    this.read.reload();
  }

  /** 先確認身分（需要時），再執行 `then`。 */
  stepUp(then: () => void) {
    const security = this.security();
    if (!security || security.readOnly) return;
    if (!security.reauthenticationRequired) return then();
    this.after = then;
    this.dialog.set('reauthenticate');
  }

  open(dialog: Exclude<SecurityDialog, 'reauthenticate'>) {
    this.stepUp(() => this.dialog.set(dialog));
  }

  close() {
    this.dialog.set(null);
    this.after = null;
  }

  async reauthenticate(body: ReauthenticationRequest) {
    this.apply(await this.api.reauthenticate(body));
    const then = this.after;
    this.after = null;
    this.dialog.set(null);
    then?.();
  }

  sendCode = () => this.api.sendCode();
  code = (id: string) => this.api.code(id);
  requestEmail = (email: string) => this.guard('email', () => this.api.requestEmail(email));

  async confirmEmail(id: string, code: string) {
    this.apply(await this.api.confirmEmail(id, code));
    this.close();
    this.toast.success('通知信箱已驗證', '其他裝置上的登入已登出。');
  }

  async setLocalLogin(body: LocalLoginRequest) {
    const changed = await this.guard('local-login', () => this.api.setLocalLogin(body));
    if (!changed) return;
    this.apply(changed);
    this.close();
    this.toast.success('本地登入已更新', '其他裝置上的登入已登出。');
  }

  disableLocalLogin() {
    this.stepUp(async () => {
      this.disabling.set(true);
      try {
        const changed = await this.guard(null, () => this.api.disableLocalLogin());
        if (!changed) return;
        this.apply(changed);
        if (!changed.signedOut) this.toast.success('已停用本地登入');
      } catch (error) {
        this.toast.show({
          tone: 'danger',
          title: '無法停用本地登入',
          description: safeMessage(error),
        });
      } finally {
        this.disabling.set(false);
      }
    });
  }

  /** 步升在這段期間過期時，回到「確認身分」並在成功後重做。 */
  private async guard<T>(
    dialog: SecurityDialog | null,
    action: () => Promise<T>,
  ): Promise<T | undefined> {
    try {
      return await action();
    } catch (error) {
      if (!(error instanceof ApiError) || error.code !== 'reauthentication_required') throw error;
      const security = this.security();
      if (security) this.security.set({ ...security, reauthenticationRequired: true });
      this.after = dialog ? () => this.dialog.set(dialog) : () => void action();
      this.dialog.set('reauthenticate');
      return undefined;
    }
  }

  private apply(changed: AccountSecurityChangeDto) {
    this.security.set(changed.security);
    if (changed.csrfToken) this.auth.renewed(changed.csrfToken);
    if (changed.signedOut) void this.signedOut();
  }

  /** 停用的正是目前的本地登入：伺服器已登出，回到登入頁。 */
  private async signedOut() {
    this.close();
    await this.auth.load();
    await this.router.navigate(['/login']);
  }
}
