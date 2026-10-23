import type { DirectoryPersonDetailDto } from '@core/api/schema';
import { formatDate, formatNumber, relativeTime } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';
import type { PropertyGroup, PropertyItem } from '@shared/ui/property-list';

interface AccountFlag {
  bit: number;
  label: string;
  tone: BadgeTone;
}

const DONT_EXPIRE_PASSWORD = 0x1_0000;

/**
 * `userAccountControl` 的位元（後端已併入即時計算的「已鎖定」與「密碼已過期」）。順序是畫面上的順序：
 * 先影響能否登入的，再是安全上要注意的設定，最後是一般屬性。`PASSWD_CANT_CHANGE`（0x40）其實記在 ACL、
 * 不會出現在這個值裡，所以不列。
 */
const ACCOUNT_FLAGS: readonly AccountFlag[] = [
  { bit: 0x2, label: '已停用', tone: 'warning' },
  { bit: 0x10, label: '已鎖定', tone: 'danger' },
  { bit: 0x80_0000, label: '密碼已過期', tone: 'danger' },
  { bit: 0x20, label: '不需要密碼', tone: 'warning' },
  { bit: 0x80, label: '以可還原加密儲存密碼', tone: 'warning' },
  { bit: 0x40_0000, label: '不需 Kerberos 預先驗證', tone: 'warning' },
  { bit: 0x8_0000, label: '信任委派', tone: 'warning' },
  { bit: 0x100_0000, label: '信任驗證委派', tone: 'warning' },
  { bit: 0x20_0000, label: '只用 DES 金鑰', tone: 'warning' },
  { bit: DONT_EXPIRE_PASSWORD, label: '密碼永不過期', tone: 'neutral' },
  { bit: 0x4_0000, label: '需要智慧卡', tone: 'info' },
  { bit: 0x10_0000, label: '敏感帳號、不可委派', tone: 'neutral' },
  { bit: 0x8, label: '需要主目錄', tone: 'neutral' },
  { bit: 0x1, label: '執行登入指令碼', tone: 'neutral' },
  { bit: 0x200, label: '一般帳號', tone: 'neutral' },
];
const KNOWN = ACCOUNT_FLAGS.reduce((mask, flag) => mask | flag.bit, 0);
/** 會讓人無法登入的狀態；放在面板標題下，不必展開屬性才看得到。 */
const BLOCKING = 0x2 | 0x10 | 0x80_0000;

/** 拆開 `userAccountControl`；不認得的位元以十六進位列出，不默默丟掉。 */
export function accountFlags(value: number) {
  const flags = ACCOUNT_FLAGS.filter((flag) => value & flag.bit).map(({ label, tone }) => ({
    label,
    tone,
  }));
  const unknown = value & ~KNOWN;
  if (unknown) flags.push({ label: `其他 ${hex(unknown)}`, tone: 'neutral' });
  return flags;
}

/** 標題下的警示：停用、鎖定、密碼過期、下次登入須改密碼、帳號已到期。 */
export function accountAlerts(detail: DirectoryPersonDetailDto, now = Date.now()) {
  const alerts = accountFlags(detail.accountControl & BLOCKING);
  if (detail.passwordMustChange) alerts.push({ label: '下次登入須變更密碼', tone: 'warning' });
  if (detail.accountExpires && Date.parse(detail.accountExpires) <= now)
    alerts.push({ label: '帳號已到期', tone: 'danger' });
  return alerts;
}

/**
 * AD 屬性面板的分組：先是「這是誰、在哪裡」（身分、組織），再是「能不能用」（帳號狀態、登入活動），
 * 最後是群組與只在對照工具時才需要的識別碼。括號內的 LDAP 名稱方便與 AD 工具對照。
 */
export function attributeGroups(
  detail: DirectoryPersonDetailDto,
  now = Date.now(),
): PropertyGroup[] {
  const flags = detail.accountControl;
  const time = (value: string | null | undefined) => (value ? formatDate(value) : null);
  // 一個月內寫「3 天前」；更早的 relativeTime 會退回日期，與值重複，就不寫。
  const ago = (value: string | null | undefined) => {
    const text = value ? relativeTime(value, now) : '';
    return text && !/^\d{4}-/.test(text) ? text : undefined;
  };
  const passwordExpires = detail.passwordMustChange
    ? '下次登入時須變更'
    : flags & DONT_EXPIRE_PASSWORD
      ? '永不過期'
      : (time(detail.passwordExpires) ?? '永不過期');
  return [
    group('identity', '身分', [
      { key: 'displayName', label: '顯示名稱', value: detail.displayName },
      { key: 'givenName', label: '名', value: detail.givenName },
      { key: 'sn', label: '姓', value: detail.surname },
      { key: 'sAMAccountName', label: '帳號', value: detail.account },
      { key: 'userPrincipalName', label: 'UPN', value: detail.upn },
      { key: 'mail', label: '信箱', value: detail.email },
      { key: 'employeeID', label: '員工編號', value: detail.employeeId },
      { key: 'description', label: '描述', value: detail.description },
    ]),
    group('organization', '組織', [
      { key: 'company', label: '公司', value: detail.company },
      { key: 'department', label: '部門', value: detail.department },
      { key: 'title', label: '職稱', value: detail.title },
      { key: 'manager', label: '主管', value: detail.manager },
      { key: 'physicalDeliveryOfficeName', label: '辦公室', value: detail.office },
      { key: 'telephoneNumber', label: '電話', value: detail.phone },
      { key: 'mobile', label: '手機', value: detail.mobile },
    ]),
    group('account', '帳號狀態', [
      {
        key: 'userAccountControl',
        label: '帳號控制',
        value: `${flags}（${hex(flags)}）`,
        badges: accountFlags(flags),
      },
      { key: 'accountExpires', label: '帳號到期', value: time(detail.accountExpires) ?? '永不' },
      {
        key: 'pwdLastSet',
        label: '密碼最後設定',
        value: detail.passwordMustChange ? '下次登入時須變更' : time(detail.passwordLastSet),
        hint: detail.passwordMustChange ? undefined : ago(detail.passwordLastSet),
      },
      { key: 'msDS-UserPasswordExpiryTimeComputed', label: '密碼到期', value: passwordExpires },
      { key: 'lockoutTime', label: '鎖定時間', value: time(detail.lockoutTime) },
    ]),
    group(
      'activity',
      '登入活動',
      [
        {
          key: 'lastLogon',
          label: '最近登入',
          // 只有這台 DC 的紀錄：空的只代表沒在這台登入過，不能說「從未登入」。
          value: time(detail.lastLogon) ?? '這台 DC 沒有紀錄',
          hint: ago(detail.lastLogon),
        },
        {
          key: 'lastLogonTimestamp',
          label: '最近登入（複寫）',
          value: time(detail.lastLogonTimestamp) ?? '從未登入',
          hint: '每台 DC 都有，但預設最多落後 14 天，只適合判斷帳號是否還在使用',
        },
        {
          key: 'logonCount',
          label: '登入次數',
          value: detail.logonCount === null ? null : formatNumber(detail.logonCount),
        },
        {
          key: 'badPwdCount',
          label: '密碼錯誤次數',
          value: detail.badPasswordCount === null ? null : formatNumber(detail.badPasswordCount),
        },
        {
          key: 'badPasswordTime',
          label: '最近密碼錯誤',
          value: time(detail.badPasswordTime),
          hint: ago(detail.badPasswordTime),
        },
      ],
      '除了「最近登入（複寫）」，這裡都是這次連到的網域控制站上的值，不會在 DC 之間複寫；使用者在別台 DC 登入時會比實際舊。',
    ),
    group('groups', '所屬群組', [
      {
        key: 'memberOf',
        label: `群組（${detail.groups.length}${detail.moreGroups ? '+' : ''}）`,
        list: detail.groups,
        hint: detail.moreGroups
          ? `只列出前 ${detail.groups.length} 個；主要群組（Domain Users）不在這個屬性裡`
          : '主要群組（Domain Users）不在這個屬性裡',
      },
    ]),
    group('object', '物件', [
      { key: 'whenCreated', label: '建立時間', value: time(detail.whenCreated) },
      {
        key: 'whenChanged',
        label: '最後異動',
        value: time(detail.whenChanged),
        hint: ago(detail.whenChanged),
      },
      { key: 'ou', label: '位置', value: detail.path || null },
      { key: 'distinguishedName', label: '辨別名稱', value: detail.dn, copy: true },
      { key: 'objectSid', label: 'SID', value: detail.sid, copy: true },
      { key: 'objectGUID', label: 'GUID', value: detail.objectGuid, copy: true },
    ]),
  ];
}

function group(id: string, label: string, items: PropertyItem[], note?: string): PropertyGroup {
  return { id, label, items, note };
}

function hex(value: number) {
  return '0x' + (value >>> 0).toString(16).toUpperCase().padStart(8, '0');
}
