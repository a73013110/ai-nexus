import { cursorPaginationStep, queryBarStep } from '@shared/ui/data-workspace-tour';
import type { ProductTourDefinition } from '@shared/ui/product-tour';

const PAGE = 'nx-mail-messages-page';
const DRAWER = `${PAGE} nx-detail-drawer`;

/** 郵件寄送紀錄頁的導覽；重試相關的動作依 `mail.messages.resend` 出現。 */
export function mailMessagesTour(options: {
  /** 有 `mail.messages.resend`：可切換自動重試，詳情可立即重試或重寄。 */
  resend: boolean;
  /** 系統日誌的顯示名稱；沒有 `diagnostics.logs` 授權時為 null。 */
  logs: string | null;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${PAGE} .feature-title`,
        title: '寄送紀錄回答什麼',
        description: `系統信都先排入佇列再由背景寄出，這裡逐封記下等待、寄送中、已寄出或失敗與每次嘗試；「已寄出」只代表郵件伺服器已接受，不代表已進收件匣${
          options.logs ? `。非預期失敗的查證代碼可到「${options.logs}」查` : ''
        }。有進行中的信時自動更新，不必重新整理。`,
        side: 'bottom',
      },
      {
        target: `${PAGE} .mail-diagnose`,
        title: '連線診斷',
        description:
          '寄不出時先查這裡。\n以目前設定跑一次 SMTP 對話但不寄信，逐步標出連線、TLS、憑證或帳密在哪一步失敗。\n傳輸未啟用時不能使用。',
        side: 'bottom',
      },
      options.resend && {
        target: `${PAGE} .mail-auto-retry`,
        title: '自動重試',
        description:
          '開啟：暫時性失敗會在 1、5、15、30、60 分鐘後自動再寄，直到放棄時間。\n關閉：第一次失敗就結束，需要時手動重試或重寄。\n下一輪寄送就生效。',
        side: 'bottom',
      },
      {
        target: `${PAGE} .mail-test`,
        title: '寄信測試',
        description:
          '選一封已登記的系統信，參數預填範例值，照常排隊寄出，主旨加上「[測試]」；結果出現在第一筆紀錄。',
        side: 'bottom',
      },
      {
        target: `${PAGE} nx-view-switch`,
        title: '依狀態切換',
        description: '等待中、寄送中、已寄出、失敗與已取消；點選立即套用，不必按「查詢」。',
        side: 'bottom',
      },
      queryBarStep(PAGE, {
        fields: '完整收件地址／訊息或批次 ID／Message-ID／主旨開頭搜尋、分類與時間範圍',
      }),
      {
        target: `${PAGE} .mail-table thead`,
        title: '寄送紀錄',
        description: `由新到舊列出狀態、遮罩後的收件者、主旨與嘗試次數；「等待重試」是失敗過、等下次嘗試的信，失敗會附原因。點主旨或整列開啟詳情${
          options.resend ? '，可立即重試或重寄' : ''
        }。`,
        side: 'bottom',
      },
      cursorPaginationStep(PAGE, { sortable: false }),
    ],
  };
}

/** 郵件詳情抽屜的導覽；立即重試與重寄只會出現其中一個，依授權與這封信的狀態而定。 */
export function mailDetailTour(options: {
  /** 有授權時這封信可用的動作：等待中可立即重試、已結束且內容還在可重寄；都不能時為 null。 */
  action: 'retry' | 'resend' | null;
  /** 這封信已有寄送嘗試。 */
  attempts: boolean;
  /** 系統日誌的顯示名稱；沒有 `diagnostics.logs` 授權時為 null。 */
  logs: string | null;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${DRAWER} .ui-drawer-summary`,
        title: '郵件摘要',
        description: '狀態、入列時間、主旨與遮罩後的收件者。',
        side: 'bottom',
      },
      options.action === 'retry' && {
        target: `${DRAWER} .mail-retry`,
        title: '立即重試',
        description:
          '不等下次嘗試時間，現在就寄同一封（Message-ID 不變）；郵件伺服器暫停寄送中也會一併恢復。',
        side: 'bottom',
      },
      options.action === 'resend' && {
        target: `${DRAWER} .mail-resend`,
        title: '重寄',
        description: '以原內容建立一封新信，原紀錄不變，新信記下重寄來源；連點也只會建立一封。',
        side: 'bottom',
      },
      options.attempts && {
        target: `${DRAWER} .mail-attempts`,
        title: '寄送嘗試',
        description: `每次嘗試的結果、SMTP 狀態碼、失敗在哪一步與遮罩後的伺服器回應${
          options.logs ? `；非預期失敗附查證代碼，可到「${options.logs}」查` : ''
        }。`,
      },
      {
        target: `${DRAWER} .mail-body-heading`,
        title: '內文與保存期限',
        description:
          '一般信可檢視寄出的內文；驗證碼等敏感信不保存可檢視的內文。\n內文、收件原址與紀錄，依保存期限分別清除。',
      },
      {
        target: `${DRAWER} .mail-relations`,
        title: '關聯識別',
        description:
          '訊息 ID、Message-ID、批次與 TraceId 可直接複製；Message-ID 就是信件標頭的值，可拿去郵件伺服器查投遞結果。',
      },
      {
        target: `${DRAWER} nx-drawer-pager`,
        title: '上一筆／下一筆',
        description: '在目前這一頁的信之間切換，保留目前的分頁。',
        side: 'bottom',
      },
    ],
  };
}
