import type {
  UnitImportAction,
  UnitImportChangeDto,
  UnitImportField,
  UnitImportIssueCode,
  UnitImportIssueDto,
  UnitImportPreviewDto,
  UnitImportResultDto,
} from '@core/api/schema';
import type { BadgeTone } from '@shared/ui/count-badge';

export const ACTION_LABELS: Record<UnitImportAction, string> = {
  Create: '新增',
  Adopt: '綁定手建單位',
  Update: '更新',
  Disable: '停用',
};

export const ACTION_TONES: Record<UnitImportAction, BadgeTone> = {
  Create: 'success',
  Adopt: 'info',
  Update: 'info',
  Disable: 'warning',
};

const FIELD_LABELS: Record<UnitImportField, string> = {
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
    const fields = change.fields.map((field) => FIELD_LABELS[field]);
    if (fields.length) parts.push(`${fields.join('、')}依公文系統更新`);
  }
  if (change.previousPath?.length) parts.push(`原為 ${change.previousPath.join(' › ')}`);
  if (!change.enabled && change.action !== 'Disable') parts.push('公文系統已關閉此部門');
  if (change.action === 'Disable') parts.push('公文系統已沒有此部門，保留成員與歷史');
  return parts.join('；');
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
