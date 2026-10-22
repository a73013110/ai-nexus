import type { DirectoryChange, DirectoryOutcomeDto, DirectoryPersonDto } from '@core/api/schema';
import type { BadgeTone } from '@shared/ui/count-badge';

/** 伺服器一次最多處理 100 人（`DirectoryProvisioning.MaxBatch`）。 */
export const DIRECTORY_BATCH_LIMIT = 100;

/** 會寫入的結果；其餘只是回報（略過、找不到、衝突）。 */
const writes = new Set(['create', 'bind', 'update']);
export const writesUser = (item: DirectoryOutcomeDto) => writes.has(item.outcome);

const outcomes: Record<string, { label: string; tone: BadgeTone }> = {
  create: { label: '新建', tone: 'success' },
  bind: { label: '綁定預建帳號', tone: 'success' },
  update: { label: '更新', tone: 'info' },
  unchanged: { label: '已是最新', tone: 'neutral' },
  disabled: { label: 'AD 已停用，未匯入', tone: 'warning' },
  conflict: { label: '帳號衝突', tone: 'danger' },
  invalid_account: { label: '帳號名不支援', tone: 'danger' },
  deleted: { label: '平台帳號已刪除', tone: 'warning' },
  not_found: { label: 'AD 找不到', tone: 'warning' },
  not_linked: { label: '未綁定 AD', tone: 'neutral' },
  failed: { label: '未完成', tone: 'danger' },
};
export const outcomeLabel = (outcome: string) => outcomes[outcome]?.label ?? outcome;
export const outcomeTone = (outcome: string): BadgeTone => outcomes[outcome]?.tone ?? 'neutral';

const states: Record<string, { label: string; tone: BadgeTone }> = {
  new: { label: '未匯入', tone: 'neutral' },
  linked: { label: '已匯入', tone: 'success' },
  prebuilt: { label: '預建帳號', tone: 'info' },
  conflict: { label: '帳號衝突', tone: 'danger' },
  invalid_account: { label: '帳號名不支援', tone: 'danger' },
  deleted: { label: '已刪除', tone: 'warning' },
};
export const stateLabel = (state: string) => states[state]?.label ?? state;
export const stateTone = (state: string): BadgeTone => states[state]?.tone ?? 'neutral';
/** 衝突、帳號名不支援與已刪除的人送出也只會被略過，不讓管理員勾選。 */
export const selectable = (person: DirectoryPersonDto) =>
  person.state === 'new' || person.state === 'linked' || person.state === 'prebuilt';

const fields: Record<string, string> = {
  account: '完整帳號',
  adAccount: 'AD 帳號',
  displayName: '姓名',
  email: '通知信箱',
};
/** `姓名：林可蘿 → 王可蘿`；空值寫「（無）」。 */
export const describeChange = (change: DirectoryChange) =>
  `${fields[change.field] ?? change.field}：${change.before || '（無）'} → ${change.after || '（無）'}`;

/** 結果摘要，依固定順序列出有人數的類別：「新建 2、更新 1、AD 找不到 1」。 */
export function summarize(items: readonly DirectoryOutcomeDto[]) {
  const counts = new Map<string, number>();
  for (const item of items) counts.set(item.outcome, (counts.get(item.outcome) ?? 0) + 1);
  return Object.keys(outcomes)
    .filter((outcome) => counts.has(outcome))
    .map((outcome) => `${outcomeLabel(outcome)} ${counts.get(outcome)}`)
    .join('、');
}

/** 結果列要顯示的姓名與帳號；目錄已找不到的人沿用選取時的資料。 */
export function outcomeName(
  item: DirectoryOutcomeDto,
  known?: ReadonlyMap<string, DirectoryPersonDto>,
) {
  const person = known?.get(item.sid);
  return {
    displayName: item.displayName || person?.displayName || item.sid,
    account: item.account || person?.account || '',
  };
}

/** 結果的補充說明：要管理員注意、但不會自動處理的情況。 */
export function outcomeNote(item: DirectoryOutcomeDto) {
  if (item.outcome === 'failed') return '資料在預覽後有變動，請重新預覽。';
  if (item.directoryDisabled && item.enabled)
    return 'AD 帳號已停用，但平台帳號仍啟用（含本地密碼）；需要時請在使用者詳情停用。';
  if (item.outcome === 'not_found' && item.enabled)
    return 'AD 已找不到（可能已刪除或不在搜尋範圍），平台帳號仍啟用。';
  return '';
}

/**
 * 匯入對話框的已選清單：跨多次搜尋累積，以 SID 為鍵保留選取當下的資料（預覽與結果在目錄找不到時用它顯示姓名）。
 * 不可變：每次變更回傳新的 Map，放進 signal 即可觸發畫面更新。
 */
export type DirectorySelection = ReadonlyMap<string, DirectoryPersonDto>;

/**
 * 勾選或取消多位（單人也用這個）；只加入可選的人，最多 {@link DIRECTORY_BATCH_LIMIT} 人，
 * 超過上限的不加入，回傳的 `skipped` 讓畫面提示。
 */
export function selectPeople(
  selection: DirectorySelection,
  people: readonly DirectoryPersonDto[],
  checked: boolean,
) {
  const next = new Map(selection);
  let skipped = 0;
  for (const person of people) {
    if (!checked) next.delete(person.sid);
    else if (next.has(person.sid) || !selectable(person)) continue;
    else if (next.size >= DIRECTORY_BATCH_LIMIT) skipped++;
    else next.set(person.sid, person);
  }
  return { selection: next as DirectorySelection, skipped };
}

/** 這次搜尋結果中可選的人是否都已勾選（沒有可選的人時為 false）。 */
export function selectsAll(selection: DirectorySelection, people: readonly DirectoryPersonDto[]) {
  const candidates = people.filter(selectable);
  return candidates.length > 0 && candidates.every((person) => selection.has(person.sid));
}
