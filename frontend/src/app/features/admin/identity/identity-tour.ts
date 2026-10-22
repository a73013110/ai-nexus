import type { ProductTourDefinition, TourStep, TourSteps } from '@shared/ui/product-tour';

/** 使用者管理頁的導覽；測試身分期間新增與列操作都被停用，改說明唯讀。 */
export function usersTour(options: {
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
  /** 有 `identity.users.import`：頁首有「從 AD 匯入」。 */
  directoryImport?: boolean;
}): ProductTourDefinition {
  const steps: TourSteps = [
    {
      target: 'nx-users-page .feature-title',
      title: '管理身分與 AI 政策',
      description:
        '每位使用者的登入方式、角色、模型政策與附件容量都在這裡設定；角色決定他能用哪些功能。',
      side: 'bottom',
    },
    {
      target: 'nx-users-page nx-admin-users-tab .page-toolbar nx-search-field',
      title: '找到使用者',
      description: '輸入姓名、登入帳號或通知信箱的任一段，列表會重新查詢。',
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-users-page nx-admin-users-tab .page-toolbar .primary-button',
      title: '新增使用者',
      description:
        '只需填帳號與角色；預建的 AD 帳號在首次登入時才綁定。模型政策與容量建立後再從詳情調整。',
      side: 'bottom',
    },
    !options.testing &&
      options.directoryImport && {
        target: 'nx-users-page nx-admin-users-tab .page-toolbar .directory-import-button',
        title: '從 AD 匯入',
        description:
          '搜尋公司 AD、跨多次搜尋勾選要匯入的人，預覽後再確認；已在平台的人會以 AD 的姓名、信箱與帳號更新。',
        side: 'bottom',
      },
    {
      target: 'nx-users-page .admin-users-table th[nxTableColumn="usage"]',
      title: '最近 30 天用量',
      description: '列出生成次數、模型回報的 tokens 與對話數，未回報的 token 不估算。',
      side: 'bottom',
    },
    {
      target: 'nx-users-page .admin-users-table .ui-table-link',
      title: '開啟使用者詳情',
      description: options.testing
        ? '點姓名可查看帳號、角色、政策與唯讀對話；測試身分期間全部唯讀。'
        : '點姓名開啟詳情，在頁籤中編輯帳號、通知信箱、角色、AI 模型與附件容量，或查看唯讀對話。',
    },
    !options.testing && {
      target: 'nx-users-page .admin-users-table nx-action-menu',
      title: '測試身分與刪除',
      description:
        '可用這位使用者的身分檢視平台；刪除後無法登入，但對話、稽核與登入帳號會保留，避免他人接管。',
      side: 'left',
    },
  ];
  return { steps };
}

/**
 * 使用者詳情對話框的導覽。開在「對話」頁籤時改為介紹受稽核的唯讀對話檢視，
 * 其他頁籤介紹摘要、頁籤與上下筆；稽核反查只在有活動稽核授權時出現。
 */
export function userInspectorTour(options: {
  /** 檢視者有活動稽核授權，摘要列出反查連結。 */
  audit: boolean;
  /** 正以測試身分檢視，各頁籤唯讀。 */
  testing: boolean;
  /** 目前在「對話」頁籤。 */
  conversations: boolean;
}): ProductTourDefinition {
  const auditStep: TourStep | false = options.audit && {
    target: 'nx-admin-user-inspector .inspector-audit',
    title: '反查活動稽核',
    description: '帶著使用者 ID 前往活動稽核，查看這位使用者的操作紀錄；對話框會直接關閉。',
    side: 'bottom',
  };
  const steps: TourSteps = options.conversations
    ? [
        {
          target: 'nx-admin-user-inspector nx-admin-user-conversations .conversation-audit-note',
          title: '受稽核的唯讀檢視',
          description:
            '每次列出清單與開啟內容都會先寫入稽核；這裡不能修改對話，附件只顯示名稱與大小，不增加下載權限。',
          side: 'bottom',
        },
        {
          target:
            'nx-admin-user-inspector nx-admin-user-conversations .conversation-tools nx-search-field',
          title: '搜尋對話',
          description: '依標題與對話內容搜尋。',
          side: 'bottom',
        },
        {
          target:
            'nx-admin-user-inspector nx-admin-user-conversations .conversation-filter nx-checkbox',
          title: '包含已刪除對話',
          description: '預設連使用者已刪除的對話一起列出，並以標籤區分已刪除與已封存。',
          side: 'bottom',
        },
        {
          target: 'nx-admin-user-inspector nx-admin-user-conversations .conversation-item',
          title: '切換對話',
          description: '點清單中的對話在右側閱讀；清單每頁 50 段，超過時下方會出現分頁。',
          side: 'right',
        },
        {
          target: 'nx-admin-user-inspector nx-admin-user-conversations .conversation-reading',
          title: '完整的對話內容',
          description:
            '依時間列出資料庫保存的所有版本與分支，包含對話指示與每次回答的耗時；每次載入 100 則。',
          side: 'left',
        },
        auditStep,
      ]
    : [
        {
          target: 'nx-admin-user-inspector nx-admin-user-summary',
          title: '這位使用者的概況',
          description: '登入方式、角色、所屬單位與最近 30 天的生成、tokens 與附件用量。',
          side: 'bottom',
        },
        auditStep,
        {
          target: 'nx-admin-user-inspector .admin-inspector nx-tabs',
          title: '依頁籤管理',
          description: options.testing
            ? '帳號、通知信箱、角色、AI 模型與附件容量分頁呈現；測試身分期間全部唯讀。'
            : '每個頁籤各自儲存；切換頁籤、換人或關閉時，未儲存的變更會先確認。「對話」頁籤可唯讀查看他的對話。',
          side: 'bottom',
        },
        {
          target: 'nx-admin-user-inspector nx-drawer-pager',
          title: '上一位、下一位',
          description: '在列表目前這一頁的使用者之間切換，不必回到列表。',
          side: 'bottom',
        },
      ];
  return { steps };
}
