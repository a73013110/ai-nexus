import type {
  UnitImportAction,
  UnitImportChangeDto,
  UnitImportField,
  UnitImportIssueCode,
  UnitImportIssueDto,
  UnitImportManagerDto,
  UnitImportPreviewDto,
  UnitImportResultDto,
} from '@core/api/schema';
import type { ChoiceGroup } from '@shared/ui/choice-groups';

const ACTION_LABELS: Record<UnitImportAction, string> = {
  Create: '新增',
  Adopt: '綁定手建單位',
  Update: '更新',
  Disable: '停用',
};
/** 勾選清單的分組順序；只有主管變動、單位本身不變的部門排最後。 */
const GROUPS: readonly (UnitImportAction | 'Managers')[] = [
  'Create',
  'Adopt',
  'Update',
  'Disable',
  'Managers',
];

/** 依公文系統更新的欄位；首長單位層級是本地設定，只會因搬移而清除，另外說明。 */
const FIELD_LABELS: Record<Exclude<UnitImportField, 'HeadRank'>, string> = {
  Name: '名稱',
  Parent: '上層',
  Enabled: '啟用狀態',
  SortOrder: '順序',
};

const ISSUE_LABELS: Record<UnitImportIssueCode, string> = {
  NameMissing: '名稱空白，略過',
  NameInvalid: '名稱過長或含控制字元，略過',
  NameDuplicate: '同一層已有同名單位，略過',
  Cycle: '上層關係成環，略過',
  DepthExceeded: '超過八層，略過',
  ParentSkipped: '上層被略過，這個部門也略過',
  AccountNotFound: '主管帳號在公文系統不存在',
  AccountDisabled: '主管的公文帳號已停用',
  UserNotFound: '主管對不到平台使用者（可能還沒登入過）',
  UserAmbiguous: '主管對到不只一位使用者',
  ManagersByHeadUnit: '主管由首長單位擔任，不同步公文系統的主管',
};

/** 預覽各類變動的數量，給摘要列。 */
export function importCounts(preview: UnitImportPreviewDto) {
  const count = (action: UnitImportAction) =>
    preview.units.filter((unit) => unit.action === action).length;
  return {
    create: count('Create'),
    adopt: count('Adopt'),
    update: count('Update'),
    disable: count('Disable'),
    managersAdded: preview.managers.filter((manager) => manager.added).length,
    managersRemoved: preview.managers.filter((manager) => !manager.added).length,
    issues: preview.issues.length,
  };
}

/** 套用會不會改到任何東西；只有問題、沒有變動時不必套用。 */
export function hasChanges(preview: UnitImportPreviewDto) {
  return preview.units.length > 0 || preview.managers.length > 0;
}

/** 單位變動的補充說明：改了哪些欄位、改名或搬移前的位置。 */
export function changeNote(change: UnitImportChangeDto) {
  const parts: string[] = [];
  if (change.action === 'Update' || change.action === 'Adopt') {
    const fields = change.fields.flatMap((field) =>
      field === 'HeadRank' ? [] : [FIELD_LABELS[field]],
    );
    if (fields.length) parts.push(`${fields.join('、')}依公文系統更新`);
    if (change.fields.includes('HeadRank')) parts.push('搬到別的上層，取消首長單位');
  }
  if (change.previousPath?.length) parts.push(`原為 ${change.previousPath.join(' › ')}`);
  if (!change.enabled && change.action !== 'Disable') parts.push('公文系統已關閉此部門');
  if (change.action === 'Disable') parts.push('公文系統已沒有此部門，保留成員與歷史');
  return parts.join('；');
}

/** 匯入預覽裡的一個部門：單位變動與主管變動合成一個可勾選項目，`id` 是 `gdDepNo`。 */
export interface ImportDepartment {
  id: string;
  group: UnitImportAction | 'Managers';
  label: string;
  description: string;
  /** 本地已停用、公文系統仍啟用：套用會重新啟用，預設不勾選。 */
  reenables: boolean;
}

function managerNote(manager: UnitImportManagerDto) {
  const name = manager.displayName || manager.account;
  if (!manager.added) return `${name} 取消主管（保留成員）`;
  return `${name} 設為${manager.deputy ? '主管（副主管）' : '主管'}${manager.joinsUnit ? '並加入為成員' : ''}`;
}

/** 依部門整理預覽：一個部門一項，順序依分組、再依預覽原本的排列。 */
export function importDepartments(preview: UnitImportPreviewDto): ImportDepartment[] {
  const managers = new Map<number, UnitImportManagerDto[]>();
  for (const manager of preview.managers)
    managers.set(manager.gdDepNo, [...(managers.get(manager.gdDepNo) ?? []), manager]);
  const items: ImportDepartment[] = preview.units.map((change) => {
    const reenables =
      (change.action === 'Update' || change.action === 'Adopt') &&
      change.enabled &&
      change.fields.includes('Enabled');
    return {
      id: String(change.gdDepNo),
      group: change.action,
      label: change.path.join(' › '),
      description: [
        reenables ? '這裡已停用，套用會重新啟用' : '',
        changeNote(change),
        ...(managers.get(change.gdDepNo) ?? []).map(managerNote),
      ]
        .filter(Boolean)
        .join('；'),
      reenables,
    };
  });
  const listed = new Set(preview.units.map((change) => change.gdDepNo));
  for (const [key, list] of managers) {
    if (listed.has(key)) continue;
    items.push({
      id: String(key),
      group: 'Managers',
      label: list[0].path.join(' › '),
      description: list.map(managerNote).join('；'),
      reenables: false,
    });
  }
  return GROUPS.flatMap((group) => items.filter((item) => item.group === group));
}

/** 給 `ChoiceGroups` 的分組；沒有項目的分組由元件略過。 */
export function importChoices(departments: readonly ImportDepartment[]): ChoiceGroup[] {
  return GROUPS.map((group) => ({
    id: group,
    name: group === 'Managers' ? '只有主管變動' : ACTION_LABELS[group],
    options: departments
      .filter((item) => item.group === group)
      .map(({ id, label, description }) => ({ id, label, description })),
  }));
}

export function issueLabel(issue: UnitImportIssueDto) {
  return ISSUE_LABELS[issue.code];
}

/** 套用後的提示。 */
export function resultSummary(result: UnitImportResultDto) {
  const units = [
    result.created && `新增 ${result.created}`,
    result.adopted && `綁定 ${result.adopted}`,
    result.updated && `更新 ${result.updated}`,
    result.disabled && `停用 ${result.disabled}`,
  ].filter(Boolean);
  const managers = [
    result.managersAdded && `設定 ${result.managersAdded}`,
    result.managersRemoved && `取消 ${result.managersRemoved}`,
  ].filter(Boolean);
  const parts = [
    units.length ? `單位${units.join('、')} 個` : '',
    managers.length ? `主管${managers.join('、')} 位` : '',
  ].filter(Boolean);
  return parts.length ? `已從公文系統匯入：${parts.join('，')}` : '組織已與公文系統一致';
}
