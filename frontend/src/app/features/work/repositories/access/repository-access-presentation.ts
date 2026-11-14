import type { ManagedRepositoryDto, RepositoryGrantDto, RepositorySyncDto } from '@core/api/schema';
import { formatDate, formatDateTimeInput, formatDay, relativeTime } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';

interface Presented {
  label: string;
  tone: BadgeTone;
}

/** 與後端 `RepositoryAccessRequest.ReasonMaxLength`、`RepositoryGrant.ReasonMaxLength`、`LoginMaxLength`、`SaveManagedRepositorySettings.MaxStewards` 相同。 */
export const REASON_MAX = 500;
export const LOGIN_MAX = 100;
export const MAX_STEWARDS = 20;

/** 權限（後端 `RepositoryPermissions`）；管理只由系統管理員授予，申請只能選讀取或寫入。 */
export const PERMISSIONS: Record<string, string> = { read: '讀取', write: '寫入', admin: '管理' };
export const permissionName = (permission: string) => PERMISSIONS[permission] ?? permission;
export const REQUEST_PERMISSIONS = [
  { value: 'read', label: '讀取', description: '複製、拉取、瀏覽程式碼與議題' },
  { value: 'write', label: '寫入', description: '另可推送分支、合併與管理議題' },
];
export const GRANT_PERMISSIONS = [
  ...REQUEST_PERMISSIONS,
  { value: 'admin', label: '管理', description: '另可修改儲存庫設定與協作者' },
];

/** 授權寫到 Gitea 的狀態（後端 `RepositorySyncStates`）。 */
export const SYNC_STATES: Record<string, Presented> = {
  pending: { label: '等待同步', tone: 'info' },
  applied: { label: '已生效', tone: 'success' },
  failed: { label: '同步失敗', tone: 'danger' },
};
export const syncState = (state: string): Presented =>
  SYNC_STATES[state] ?? { label: state, tone: 'neutral' };

/** 授權的來源（後端 `RepositoryGrantSources`）。 */
export const GRANT_SOURCES: Record<string, string> = {
  request: '申請核准',
  steward: '負責人授予',
  admin: '管理員授予',
};

/** 台北時間的今天：到期日當天結束前有效，最早可選今天；伺服器以同一條規則再檢查。 */
export const today = () => formatDateTimeInput(new Date()).slice(0, 10);

/** 到期日是當天結束前有效；沒有到期日寫「不限期」。 */
export const expiryText = (expiresOn: string | null) =>
  expiresOn ? `到 ${formatDay(`${expiresOn}T00:00:00+08:00`)}` : '不限期';

/**
 * 我在這個儲存庫的狀況：有授權寫權限與到期；在 Gitea 上被直接加入（平台沒有紀錄）時照實寫出權限並標成未納管；
 * 審核中寫審核中，都沒有時看能不能申請。授權還沒寫到 Gitea 時附註，免得以為核准了卻打不開。
 */
export function myAccess(repository: ManagedRepositoryDto): Presented & { note: string } {
  const grant = repository.myGrant;
  if (grant)
    return {
      label: permissionName(grant.permission),
      tone: grant.syncState === 'failed' ? 'danger' : 'success',
      note: [
        expiryText(grant.expiresOn),
        grant.syncState === 'applied' ? '' : syncState(grant.syncState).label,
      ]
        .filter(Boolean)
        .join('・'),
    };
  if (repository.myDirectPermission)
    return {
      label: permissionName(repository.myDirectPermission),
      tone: 'warning',
      note: repository.pendingInstanceId ? '未納管・申請審核中' : '未納管：在 Gitea 上直接加入',
    };
  if (repository.pendingInstanceId) return { label: '審核中', tone: 'info', note: '' };
  return { label: '沒有權限', tone: 'neutral', note: canRequest(repository) ? '' : '不開放申請' };
}

/** 能不能送出申請：開放申請、還在 Gitea、沒有封存，也沒有審核中的申請。 */
export const canRequest = (repository: ManagedRepositoryDto) =>
  repository.requestable &&
  !repository.missing &&
  !repository.isArchived &&
  !repository.pendingInstanceId;

/** 儲存庫本身的狀態標籤；正常（開放申請的私有庫）不加標籤。 */
export function repositoryTags(repository: ManagedRepositoryDto): Presented[] {
  return [
    ...(repository.missing ? [{ label: 'Gitea 已找不到', tone: 'danger' as const }] : []),
    ...(repository.isArchived ? [{ label: '已封存', tone: 'neutral' as const }] : []),
    ...(!repository.isPrivate ? [{ label: '公開', tone: 'warning' as const }] : []),
    ...(!repository.requestable && !repository.missing
      ? [{ label: '不開放申請', tone: 'neutral' as const }]
      : []),
    // 只有負責人與管理員拿得到人數，其他人永遠是 0。
    ...(repository.unregisteredCount
      ? [{ label: `${repository.unregisteredCount} 位未納管`, tone: 'warning' as const }]
      : []),
  ];
}

/** 狀態條件；「需要處理」給管理者找出要轉成授權或指定負責人的儲存庫。 */
export type RepositoryState =
  '' | 'unregistered' | 'unassigned' | 'closed' | 'public' | 'archived' | 'missing';
export const REPOSITORY_STATES: readonly { value: RepositoryState; label: string }[] = [
  { value: '', label: '全部' },
  { value: 'unregistered', label: '有未納管協作者' },
  { value: 'unassigned', label: '未指定負責人' },
  { value: 'closed', label: '不開放申請' },
  { value: 'public', label: '公開' },
  { value: 'archived', label: '已封存' },
  { value: 'missing', label: 'Gitea 已找不到' },
];
export function matchesState(repository: ManagedRepositoryDto, state: RepositoryState) {
  switch (state) {
    case 'unregistered':
      return repository.unregisteredCount > 0;
    case 'unassigned':
      return !repository.stewards.length;
    case 'closed':
      return !repository.requestable;
    case 'public':
      return !repository.isPrivate;
    case 'archived':
      return repository.isArchived;
    case 'missing':
      return repository.missing;
    default:
      return true;
  }
}

/** 授權列的說明：來源、授予人與時間。 */
export function grantOrigin(grant: RepositoryGrantDto) {
  return [
    GRANT_SOURCES[grant.source] ?? grant.source,
    grant.grantedBy?.displayName,
    formatDate(grant.grantedAt),
  ]
    .filter(Boolean)
    .join('・');
}

/** 頁首的同步狀況：同步中、已排入，或上次同步多久前與結果。 */
export function syncSummary(sync: RepositorySyncDto | null): Presented & { title: string } {
  if (!sync) return { label: '尚未同步', tone: 'neutral', title: '' };
  if (sync.running) return { label: '同步中…', tone: 'info', title: '' };
  if (sync.requested) return { label: '已排入同步', tone: 'info', title: '' };
  if (!sync.lastFinishedAt) return { label: '尚未同步', tone: 'neutral', title: '' };
  const failed = sync.lastStatus === 'failed' || sync.lastStatus === 'interrupted';
  return {
    label: `${relativeTime(sync.lastFinishedAt)}同步${failed ? '失敗' : ''}`,
    tone: failed ? 'danger' : 'neutral',
    title: [formatDate(sync.lastFinishedAt), sync.lastSummary].filter(Boolean).join('\n'),
  };
}
