import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { AdminUserDto, UserAccountRequest } from '@core/api/schema';
import { uniqueId } from '@shared/browser/unique-id';
import { Checkbox } from '@shared/ui/checkbox';
import { Field } from '@shared/ui/field';

/**
 * 使用者編輯中的名稱與登入方式；密碼永遠不會被讀回。啟用與停用是立即生效的動作（檢視器的「帳號狀態」），
 * 不在草稿裡，免得和姓名一起被「儲存」。
 */
export interface UserAccountDraft {
  name: string;
  adEnabled: boolean;
  localEnabled: boolean;
  adAccount: string;
  localAccount: string;
  password: string;
  hasLocalPassword: boolean;
}

/** 新使用者在加入 AD 帳號之前，以本地密碼登入。 */
export function userAccountDraft(user?: AdminUserDto | null): UserAccountDraft {
  const login = user?.authentication;
  return {
    name: user?.displayName ?? '',
    adEnabled: login?.adEnabled ?? !!user,
    localEnabled: login?.localEnabled ?? !user,
    adAccount: login?.adAccount ?? '',
    localAccount: login?.localAccount ?? '',
    password: '',
    hasLocalPassword: login?.hasLocalPassword ?? false,
  };
}

/** 建立的使用者一律啟用；既有使用者沿用目前的狀態（`enabled`）。 */
export function userAccountRequest(
  draft: UserAccountDraft,
  roleIds: string[],
  enabled = true,
): UserAccountRequest {
  return {
    displayName: draft.name,
    enabled,
    adEnabled: draft.adEnabled,
    localEnabled: draft.localEnabled,
    adAccount: draft.adEnabled ? draft.adAccount : null,
    localAccount: draft.localEnabled ? draft.localAccount : null,
    password: draft.password || null,
    roleIds,
  };
}

/** 使用者如何登入，供列表與標頭使用。 */
export function signInMethods(user: AdminUserDto) {
  const login = user.authentication;
  const methods = [
    (login?.adEnabled ?? true) ? 'AD' : '',
    login?.localEnabled ? '本地密碼' : '',
  ].filter(Boolean);
  return methods.join(' · ') || '未開放登入';
}

/**
 * 信箱的來源與驗證狀態；AD 提供的信箱由目錄同步，視為已驗證。
 * 管理員設定的地址在本人驗證前不能收步升驗證碼，標示來源讓管理員知道還差本人驗證。
 */
export function emailState(user: Pick<AdminUserDto, 'emailSource' | 'emailVerifiedAt'>) {
  if (user.emailSource === 'directory') return 'AD 提供';
  if (user.emailVerifiedAt) return '已驗證';
  return user.emailSource === 'admin' ? '管理員設定・未驗證' : '未驗證';
}

export function sameUserAccount(first: UserAccountDraft, second: UserAccountDraft) {
  return (Object.keys(first) as (keyof UserAccountDraft)[]).every(
    (key) => first[key] === second[key],
  );
}

/**
 * 名稱與登入方式，分成兩個表單區段；由建立對話框與使用者檢視器共用，兩者會把它們包在 `.form-sections` 中。
 * 檢視器投影兩處：`[account-basics]` 接在姓名之後（通知信箱），`[ad-binding]` 接在 AD 帳號之後（綁定狀態與改綁）。
 * 已以 SID 綁定 AD 時（`adBound`），AD 帳號名跟著目錄、伺服器不接受在表單裡改，所以不顯示輸入框，改由投影的綁定列呈現。
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
        <label class="form-field-short"
          >姓名<input
            nxField
            aria-label="使用者姓名"
            required
            maxlength="120"
            [value]="value().name"
            [readOnly]="disabled()"
            (input)="change('name', $any($event.target).value)"
        /></label>
        <ng-content select="[account-basics]" />
      </div>
    </section>
    <section class="form-section" role="group" [attr.aria-labelledby]="id + '-login'">
      <header class="form-section-head">
        <h3 [id]="id + '-login'">登入方式</h3>
        <p class="form-note">可同時開啟，共用同一份角色、對話與個人設定。</p>
      </header>
      <div class="form-section-body">
        <nx-checkbox
          label="AD 驗證"
          description="驗證後以 AD SID 綁定，不保存 AD 密碼。"
          [checked]="value().adEnabled"
          [disabled]="disabled()"
          (checkedChange)="change('adEnabled', $event)"
        />
        @if (value().adEnabled) {
          <div class="form-subfields">
            @if (!adBound()) {
              <label class="form-field-short"
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
            }
            <ng-content select="[ad-binding]" />
          </div>
        }
        <nx-checkbox
          label="本地密碼"
          description="以 Argon2id 加鹽雜湊保存；重設後舊登入會失效。"
          [checked]="value().localEnabled"
          [disabled]="disabled()"
          (checkedChange)="change('localEnabled', $event)"
        />
        @if (value().localEnabled) {
          <div class="form-subfields form-row">
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
  readonly id = uniqueId('nx-user-account');
  readonly value = input.required<UserAccountDraft>();
  readonly disabled = input(false);
  /** 已以 SID 綁定 AD：AD 帳號不在表單裡編輯。 */
  readonly adBound = input(false);
  readonly valueChange = output<UserAccountDraft>();
  change<K extends keyof UserAccountDraft>(key: K, value: UserAccountDraft[K]) {
    this.valueChange.emit({ ...this.value(), [key]: value });
  }
}
