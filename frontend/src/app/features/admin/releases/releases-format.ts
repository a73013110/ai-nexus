import type { ReleaseStatusDto, SchemaChangeDto } from '@core/api/schema';
import type { BadgeTone } from '@shared/ui/count-badge';

export const CHANGE_NAMES: Record<string, string> = {
  added: '新增',
  modified: '修改',
  removed: '移除',
};

export const OBJECT_TYPE_NAMES: Record<string, string> = {
  table: '資料表',
  view: '檢視',
  procedure: '預存程序',
  function: '函式',
  trigger: '觸發程序',
  synonym: '同義字',
};

/** 移除最可能讓程式出錯（呼叫的物件不見了），新增最無害。 */
export const changeTone = (change: string): BadgeTone =>
  change === 'removed' ? 'danger' : change === 'modified' ? 'warning' : 'info';

export const objectName = (change: Pick<SchemaChangeDto, 'schemaName' | 'name'>) =>
  `${change.schemaName}.${change.name}`;

/** 版本對照的結論：不相容要先處理；沒有快照代表資料庫還沒以 update.sql 部署過，異動偵測尚未開始。 */
export function compatibility(status: ReleaseStatusDto): {
  label: string;
  tone: BadgeTone;
  hint: string;
} {
  if (status.compatibility === 'incompatible')
    return {
      label: '不相容',
      tone: 'danger',
      hint: status.databaseVersion
        ? `資料庫 v${status.databaseVersion} 低於程式需要的 v${status.minimumDatabaseVersion}`
        : '資料庫沒有版本紀錄',
    };
  if (status.compatibility === 'compatible')
    return { label: '相容', tone: 'success', hint: '資料庫已到程式需要的版本' };
  return { label: '未檢查', tone: 'neutral', hint: '開發中的程式不檢查資料庫版本' };
}

/** 修訂只顯示前 7 碼，完整值放在 title。 */
export const shortRevision = (revision: string | null | undefined) => revision?.slice(0, 7) ?? '';
