export type LoginMethod = 'ad' | 'local' | 'windows';

interface CredentialCopy {
  readonly account: string;
  readonly placeholder: string;
  readonly hint: string;
  readonly password: string;
}

export interface LoginMethodSpec {
  readonly label: string;
  readonly description: string;
  /** Password methods share one form; Windows sign-in is a browser challenge without one. */
  readonly credentials: CredentialCopy | null;
}

/** Every sign-in method the page can offer; the server's session decides which ones appear. */
export const LOGIN_METHODS: Readonly<Record<LoginMethod, LoginMethodSpec>> = {
  ad: {
    label: 'AD 帳號',
    description: '使用公司 AD 帳號與密碼。',
    credentials: {
      account: 'AD 帳號',
      placeholder: '公司帳號，例如 chen',
      hint: '輸入帳號名稱，無需加入網域。',
      password: 'AD 密碼',
    },
  },
  local: {
    label: '本地帳號',
    description: '使用管理者設定的本地帳號與密碼。',
    credentials: {
      account: '本地帳號',
      placeholder: '管理者設定的登入帳號',
      hint: '使用本地密碼；與 AD 密碼分開管理。',
      password: '本地密碼',
    },
  },
  windows: {
    label: 'Windows 身分',
    description: '使用公司電腦的 Windows 身分登入。',
    credentials: null,
  },
};

export function isLoginMethod(value: unknown): value is LoginMethod {
  return typeof value === 'string' && Object.hasOwn(LOGIN_METHODS, value);
}

/** The methods the server offers, in its order; unknown names from a newer server are dropped. */
export function offeredMethods(
  session: { mode: string; methods?: readonly string[] | null } | null,
) {
  const methods = (session?.methods ?? [session?.mode === 'Windows' ? 'windows' : 'ad']).filter(
    isLoginMethod,
  );
  return methods.length ? methods : (['ad'] as LoginMethod[]);
}
