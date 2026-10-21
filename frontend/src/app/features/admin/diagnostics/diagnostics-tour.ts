import { cursorPaginationStep, queryBarStep } from '@shared/ui/data-workspace-tour';
import type { ProductTourDefinition } from '@shared/ui/product-tour';

const PAGE = 'nx-system-logs-page';
const DRAWER = 'nx-log-detail-drawer nx-detail-drawer';

/** 系統日誌頁的導覽；降級提示、詳情與匯出依狀態與授權出現。 */
export function systemLogsTour(options: {
  /** 有 `diagnostics.logs.detail`：列可點開診斷詳情。 */
  detail: boolean;
  /** 有 `diagnostics.logs.export`。 */
  export: boolean;
  /** 目前這一頁的查詢結果帶有降級旗標。 */
  degraded: boolean;
  /** 即時監控的顯示名稱；沒有 `monitoring` 授權時為 null。 */
  monitoring: string | null;
  /** 活動稽核的顯示名稱；沒有 `audit` 授權時為 null。 */
  audit: string | null;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${PAGE} .feature-title`,
        title: '日誌回答什麼',
        description: `查證為什麼失敗、HTTP 狀態與耗時、例外與執行流程${
          options.audit ? `；誰在何時做了什麼請看「${options.audit}」` : ''
        }。每次查詢、詳情與匯出都會留下讀取紀錄。`,
        side: 'bottom',
      },
      options.degraded && {
        target: `${PAGE} .log-degraded`,
        title: '結果可能不完整',
        description: `日誌管線降級時，部分事件可能延遲寫入或遺失${
          options.monitoring ? `；佇列與遺失情形在「${options.monitoring}」的日誌管線卡` : ''
        }。`,
        side: 'bottom',
      },
      queryBarStep(PAGE, {
        fields: '時間範圍、等級與搜尋',
        more: '模組、事件名稱、錯誤碼與執行個體',
      }),
      {
        target: `${PAGE} .log-search`,
        title: '一個搜尋框查各種識別',
        description:
          '依格式判斷：NX- 開頭是查證代碼、32 碼 hex 是 TraceId、GUID 可切換比對 RunId／JobId／OperationId，其餘當訊息模板文字（最多 72 字、限一天內）。追查使用者回報時貼上查證代碼即可。',
        side: 'bottom',
      },
      {
        target: `${PAGE} .log-table thead`,
        title: '事件列表',
        description: `時間依你的時區顯示，點時間表頭切換新舊順序；「查證／關聯」欄可複製查證代碼並看到 Trace、Job 或 Run 識別，「欄位」可隱藏次要欄。${
          options.detail
            ? '點事件名稱或整列開啟診斷詳情。'
            : '沒有詳情權限時只看得到摘要，看不到屬性與例外堆疊。'
        }`,
        side: 'bottom',
      },
      options.export && {
        target: `${PAGE} .log-export`,
        title: '匯出 CSV',
        description:
          '由伺服器匯出整個已套用的查詢（只含摘要欄位），預設區間在按下時重算；超過範圍或筆數上限會拒絕而不截斷。',
        side: 'bottom',
      },
      cursorPaginationStep(PAGE),
    ],
  };
}

/** 日誌詳情抽屜的導覽；例外、關聯流程與稽核反查依這一筆事件的內容調整。 */
export function logDetailTour(options: {
  /** 這筆事件記錄了例外型別。 */
  exception: boolean;
  /** 這筆事件有 TraceId、JobId 或 RunId，可以列出同一流程。 */
  flow: boolean;
  /** 活動稽核的顯示名稱；沒有 `audit` 授權或事件沒有 TraceId 時為 null（不顯示反查連結）。 */
  audit: string | null;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${DRAWER} .ui-drawer-summary`,
        title: '事件摘要',
        description: '等級、發生時間、事件名稱與模組 Category。',
        side: 'bottom',
      },
      {
        target: `${DRAWER} .ui-message-box`,
        title: '事件訊息',
        description: `由訊息模板展開；你的權限看不到的參數顯示 [omitted]。${
          options.exception ? '下方的例外型別可直接跳到例外堆疊。' : ''
        }`,
      },
      {
        target: `${DRAWER} nx-tabs`,
        title: '屬性、例外與流程',
        description: `「受控屬性」列出模板欄位與遮罩後的屬性 JSON，秘密不會出現；「例外堆疊」顯示型別與堆疊。${
          options.flow
            ? '「關聯流程」依 Trace、Job 或 Run 列出本次查詢範圍內同一流程的事件，由舊到新最多 50 筆，更長的流程請用篩選繼續查。'
            : '這筆事件沒有 Trace、Job 或 Run 識別，「關聯流程」只列出它自己。'
        }`,
        side: 'bottom',
      },
      {
        target: `${DRAWER} .log-identifiers`,
        title: '關聯識別',
        description: `查證代碼、TraceId、JobId 等可直接複製，貼回搜尋框就能查同一流程${
          options.audit ? `；「查閱同一流程的活動稽核」開啟${options.audit}` : ''
        }。`,
      },
      {
        target: `${DRAWER} nx-drawer-pager`,
        title: '上一筆／下一筆',
        description: '在目前這一頁的事件之間切換，保留目前的分頁。',
        side: 'bottom',
      },
    ],
  };
}
