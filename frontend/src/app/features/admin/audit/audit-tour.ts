import { queryBarStep } from '@shared/ui/data-workspace-tour';
import type { ProductTourDefinition } from '@shared/ui/product-tour';

const PAGE = 'nx-activity-audit-page';
const DRAWER = `${PAGE} nx-detail-drawer`;

/** 活動稽核頁的導覽；只在提到系統日誌時依授權調整。 */
export function activityAuditTour(options: {
  /** 系統日誌的顯示名稱；沒有 `diagnostics.logs` 授權時為 null。 */
  logs: string | null;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${PAGE} .feature-title`,
        title: '稽核回答什麼',
        description: `誰在何時登入、操作、變更授權或查閱資料，結果如何；紀錄與業務一起寫入，不能修改或刪除${
          options.logs ? `。錯誤、耗時與技術流程請看「${options.logs}」` : ''
        }。你看得到的範圍依授權的資料範圍而定。`,
        side: 'bottom',
      },
      {
        target: `${PAGE} nx-view-switch`,
        title: '依分類切換',
        description:
          '登入與身分、管理異動、查閱與匯出、功能操作；點選立即套用，動作清單也只留該分類的動作。',
        side: 'bottom',
      },
      queryBarStep(PAGE, { fields: '帳號／資源／Trace ID／查證代碼搜尋、動作、結果與日期' }),
      {
        target: `${PAGE} .activity-audit-table thead`,
        title: '活動紀錄',
        description:
          '由新到舊列出操作者、動作、資源與結果；失敗登入的操作者顯示「未驗證」與嘗試的帳號。「欄位」可隱藏次要欄，點動作或整列開啟詳情。',
        side: 'bottom',
      },
      {
        target: `${PAGE} .audit-export`,
        title: '匯出 CSV',
        description:
          '由伺服器匯出整個已套用的查詢，不分頁，預設區間在按下時重算；超過筆數上限會拒絕而不截斷，每次匯出本身也會留下稽核紀錄。',
        side: 'bottom',
      },
      {
        target: `${PAGE} nx-table-pagination`,
        title: '翻頁',
        description:
          '紀錄固定由新到舊、不計總數，只有上一頁、下一頁與回到第一頁，新事件不會讓翻頁重複；要找較早的紀錄請縮小日期範圍。每頁筆數與目前頁不寫進網址。',
      },
    ],
  };
}

/** 稽核詳情抽屜的導覽；前後差異、流程關聯與日誌連結依這一筆紀錄的內容與授權出現。 */
export function auditDetailTour(options: {
  /** 這筆紀錄有欄位前後差異。 */
  changes: boolean;
  /** 這筆紀錄有 TraceId、OperationId 或查證代碼。 */
  flow: boolean;
  /** 系統日誌的顯示名稱；沒有授權或這筆紀錄無法帶入日誌查詢時為 null。 */
  logs: string | null;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${DRAWER} .ui-drawer-summary`,
        title: '紀錄摘要',
        description: '結果、發生時間、動作與操作者；以測試身分操作時一併標出。',
        side: 'bottom',
      },
      options.changes && {
        target: `${DRAWER} .audit-changes`,
        title: '前後差異',
        description: '授權與設定異動逐欄列出異動前與異動後，功能授權依選單位置分組顯示。',
      },
      {
        target: `${DRAWER} .audit-basics`,
        title: '基本資訊',
        description: '分類、來源等情境、資源與資源 ID、原始動作代碼與紀錄 ID。',
      },
      options.flow && {
        target: `${DRAWER} .audit-flow`,
        title: '流程關聯',
        description: `TraceId、OperationId 與查證代碼可直接複製${
          options.logs ? `；「查證相關日誌」開啟「${options.logs}」，帶入同一流程與前後五分鐘` : ''
        }。`,
      },
      {
        target: `${DRAWER} .ui-raw-record`,
        title: '原始紀錄',
        description: '展開遮罩後的原始 metadata，可核對介面沒有逐欄顯示的內容。',
      },
      {
        target: `${DRAWER} nx-drawer-pager`,
        title: '上一筆／下一筆',
        description: '在目前這一頁的紀錄之間切換，保留目前的分頁。',
        side: 'bottom',
      },
    ],
  };
}
