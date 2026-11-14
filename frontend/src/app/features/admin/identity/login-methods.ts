export type LoginMethod = 'ad' | 'local' | 'windows';

/** 帳密表單的帳號欄位提示；欄位標籤就是登入方式的名稱（`label`），密碼標籤見 `passwordLabelFor`。 */
interface AccountHints {
  readonly placeholder: string;
  readonly hint: string;
}

export interface LoginMethodSpec {
  readonly label: string;
  readonly description: string;
  /** 密碼登入方式共用一個表單；Windows 登入則是不需要表單的瀏覽器挑戰。 */
  readonly credentials: AccountHints | null;
}

/** 頁面可以提供的所有登入方式；由伺服器的工作階段決定實際出現哪些。 */
export const LOGIN_METHODS: Readonly<Record<LoginMethod, LoginMethodSpec>> = {
  ad: {
    label: 'AD 帳號',
    description: '使用公司 AD 帳號與密碼。',
    credentials: {
      placeholder: '公司帳號，例如 chen',
      hint: '只輸入帳號名稱，不需網域或 email。',
    },
  },
  local: {
    label: '本地帳號',
    description: '使用管理者設定的本地帳號與密碼。',
    credentials: {
      placeholder: '管理者設定的登入帳號',
      hint: '使用本地密碼。與 AD 密碼分開管理。',
    },
  },
  windows: {
    label: 'Windows 身分',
    description: '使用公司電腦的 Windows 身分登入。',
    credentials: null,
  },
};

/**
 * 密碼欄位的標籤，登入、解鎖與步升驗證共用：本地帳號用本地密碼，其餘（AD、Windows）以 AD 密碼驗證。
 * 傳入登入方式（`LoginMethod`）或工作階段記錄的方式；未知或空值視為 AD。
 */
export function passwordLabelFor(method: string | null | undefined) {
  return method === 'local' ? '本地密碼' : 'AD 密碼';
}

export function isLoginMethod(value: unknown): value is LoginMethod {
  return typeof value === 'string' && Object.hasOwn(LOGIN_METHODS, value);
}

/** 伺服器提供的登入方式，依其順序；較新伺服器送來的未知名稱會被丟棄。 */
export function offeredMethods(
  session: { mode: string; methods?: readonly string[] | null } | null,
) {
  const methods = (session?.methods ?? [session?.mode === 'Windows' ? 'windows' : 'ad']).filter(
    isLoginMethod,
  );
  return methods.length ? methods : (['ad'] as LoginMethod[]);
}
