import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { AdminUserDto, UserAccountRequest } from '../../../core/api/schema';
import { Checkbox } from '../../../shared/ui/checkbox';
import { Field } from '../../../shared/ui/field';

/** A user's name, switch and sign-in methods as edited; the password is never read back. */
export interface UserAccountDraft {
  name: string;
  enabled: boolean;
  adEnabled: boolean;
  localEnabled: boolean;
  adAccount: string;
  localAccount: string;
  password: string;
  hasLocalPassword: boolean;
}

/** A new user signs in with a local password until an AD account is added. */
export function userAccountDraft(user?: AdminUserDto | null): UserAccountDraft {
  const login = user?.authentication;
  return {
    name: user?.displayName ?? '',
    enabled: user?.enabled ?? true,
    adEnabled: login?.adEnabled ?? !!user,
    localEnabled: login?.localEnabled ?? !user,
    adAccount: login?.adAccount ?? user?.account.split('\\').at(-1)?.split('@')[0] ?? '',
    localAccount: login?.localAccount ?? '',
    password: '',
    hasLocalPassword: login?.hasLocalPassword ?? false,
  };
}

export function userAccountRequest(draft: UserAccountDraft, roleIds: string[]): UserAccountRequest {
  return {
    displayName: draft.name,
    enabled: draft.enabled,
    adEnabled: draft.adEnabled,
    localEnabled: draft.localEnabled,
    adAccount: draft.adEnabled ? draft.adAccount : null,
    localAccount: draft.localEnabled ? draft.localAccount : null,
    password: draft.password || null,
    roleIds,
  };
}

/** How a user may sign in, for lists and headers. */
export function signInMethods(user: AdminUserDto) {
  const login = user.authentication;
  const methods = [
    (login?.adEnabled ?? true) ? 'AD' : '',
    login?.localEnabled ? '本地密碼' : '',
  ].filter(Boolean);
  return methods.join(' · ') || '未開放登入';
}

export function sameUserAccount(a: UserAccountDraft, b: UserAccountDraft) {
  return (Object.keys(a) as (keyof UserAccountDraft)[]).every((key) => a[key] === b[key]);
}

let sequence = 0;

/**
 * Name, switch and sign-in methods as two form sections; shared by the create dialog and the user
 * inspector, which wrap them in `.form-sections`.
 */
@Component({
  selector: 'nx-user-account-fields',
  imports: [Checkbox, Field],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './user-account-fields.scss',
  template: `
    <section class="form-section" role="group" [attr.aria-labelledby]="id + '-basics'">
      <header class="form-section-head"><h3 [id]="id + '-basics'">基本資料</h3></header>
      <div class="form-section-body">
        <label class="user-name-field"
          >姓名<input
            nxField
            aria-label="使用者姓名"
            required
            maxlength="120"
            [value]="value().name"
            [readOnly]="disabled()"
            (input)="change('name', $any($event.target).value)"
        /></label>
        <nx-checkbox
          label="啟用使用者"
          description="停用後無法登入，既有工作階段會失效；資料與稽核保留。"
          [checked]="value().enabled"
          [disabled]="disabled()"
          (checkedChange)="change('enabled', $event)"
        />
      </div>
    </section>
    <section class="form-section" role="group" [attr.aria-labelledby]="id + '-login'">
      <header class="form-section-head">
        <h3 [id]="id + '-login'">登入方式</h3>
        <p class="form-note">可同時開啟，共用同一份角色、對話與個人設定。</p>
      </header>
      <div class="form-section-body">
        <nx-checkbox
          label="允許 AD 驗證"
          description="驗證後綁定 AD SID，不保存 AD 密碼。"
          [checked]="value().adEnabled"
          [disabled]="disabled()"
          (checkedChange)="change('adEnabled', $event)"
        />
        @if (value().adEnabled) {
          <div class="form-subfields login-fields">
            <label
              >AD 帳號（不含網域）<input
                nxField
                aria-label="使用者 AD 帳號"
                required
                maxlength="64"
                autocomplete="off"
                [value]="value().adAccount"
                [readOnly]="disabled()"
                (input)="change('adAccount', $any($event.target).value)"
            /></label>
          </div>
        }
        <nx-checkbox
          label="允許本地密碼登入"
          description="以 Argon2id 加鹽雜湊保存；重設後舊登入會失效。"
          [checked]="value().localEnabled"
          [disabled]="disabled()"
          (checkedChange)="change('localEnabled', $event)"
        />
        @if (value().localEnabled) {
          <div class="form-subfields login-fields">
            <label
              >本地登入帳號<input
                nxField
                aria-label="本地登入帳號"
                required
                maxlength="64"
                autocomplete="off"
                [value]="value().localAccount"
                [readOnly]="disabled()"
                (input)="change('localAccount', $any($event.target).value)"
            /></label>
            <label
              >{{ value().hasLocalPassword ? '重設本地密碼' : '初始本地密碼' }}（12–128 字元）<input
                nxField
                aria-label="設定本地密碼"
                type="password"
                autocomplete="new-password"
                minlength="12"
                maxlength="128"
                [placeholder]="value().hasLocalPassword ? '留白保留既有密碼' : ''"
                [required]="!value().hasLocalPassword"
                [value]="value().password"
                [readOnly]="disabled()"
                (input)="change('password', $any($event.target).value)"
            /></label>
          </div>
        }
      </div>
    </section>
  `,
})
export class UserAccountFields {
  readonly id = `nx-user-account-${++sequence}`;
  readonly value = input.required<UserAccountDraft>();
  readonly disabled = input(false);
  readonly valueChange = output<UserAccountDraft>();
  change<K extends keyof UserAccountDraft>(key: K, value: UserAccountDraft[K]) {
    this.valueChange.emit({ ...this.value(), [key]: value });
  }
}
