import type { ProductTourDefinition, TourStep, TourSteps } from '@shared/ui/product-tour';

/** 使用者管理頁的導覽。 */
export function usersTour(options: {
  /** 有 `identity.users.import`：頁首有「從 AD 匯入」。 */
  directoryImport?: boolean;
  /** 有 `identity.users.sync`：表格右上與列選單有「從 AD 更新」。 */
  directorySync?: boolean;
}): ProductTourDefinition {
  const sync = !!options.directorySync;
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
    options.directoryImport && {
      target: 'nx-users-page nx-admin-users-tab .page-toolbar .directory-import-button',
      title: '從 AD 匯入',
      description:
        '新進人員還沒登入、要先指派角色時使用。\n依 OU 瀏覽公司 AD，可跨節點勾選，預覽後再確認。\n已在平台的人，會以 AD 的姓名、信箱與帳號更新。',
      side: 'bottom',
    },
    {
      target: 'nx-users-page nx-admin-users-tab .page-toolbar .primary-button',
      title: '新增使用者',
      description:
        '只需填帳號與角色；預建的 AD 帳號在首次登入時才綁定。模型政策與容量建立後再從詳情調整。',
      side: 'bottom',
    },
    sync && {
      target: 'nx-users-page .admin-users-table .directory-sync-button',
      title: '從 AD 更新',
      description:
        'AD 改了姓名、信箱或帳號後使用。\n勾選本頁的人批次更新，並逐人列出結果。\nAD 已停用或找不到的人只標示，不會停用平台帳號。',
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
      description:
        '點姓名開啟詳情，在頁籤中編輯帳號、通知信箱、角色、AI 模型與附件容量，或查看唯讀對話。',
    },
    {
      target: 'nx-users-page .admin-users-table nx-action-menu',
      title: '測試身分與刪除',
      description: `${sync ? '「從 AD 更新」只更新這一位。\n' : ''}可用這位使用者的身分檢視平台。
刪除後無法登入，但對話、稽核與登入帳號會保留，避免他人接管。`,
      side: 'left',
    },
  ];
  return { steps };
}

/**
 * 使用者詳情對話框的導覽。開在「對話」頁籤時改為介紹受稽核的唯讀對話檢視，
 * 其他頁籤介紹標題列、頁籤與上下筆；稽核反查只在有活動稽核授權時出現（收在標題列的選單）。
 */
export function userInspectorTour(options: {
  /** 檢視者有活動稽核授權，標題列的選單有反查項目。 */
  audit: boolean;
  /** 目前在「對話」頁籤。 */
  conversations: boolean;
}): ProductTourDefinition {
  const menuStep: TourStep = {
    target: 'nx-admin-user-inspector .dialog-bar nx-action-menu',
    title: options.audit ? '複製 ID 與反查活動稽核' : '複製使用者 ID',
    description: options.audit
      ? '選單可複製使用者 ID，或帶著 ID 前往活動稽核查看這位使用者的操作紀錄（對話框會直接關閉）。'
      : '選單可複製使用者 ID，到其他頁面搜尋或回報問題時使用。',
    side: 'bottom',
  };
  const steps: TourSteps = options.conversations
    ? [
        {
          target: 'nx-admin-user-inspector nx-admin-user-conversations .conversation-audit-note',
          title: '受稽核的唯讀檢視',
          description:
            '列出清單、開啟內容、預覽或下載附件，都會先寫入稽核。\n這裡不能修改對話，也不能重新生成或評分。',
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
          title: '與聊天相同的對話內容',
          description:
            '先顯示使用者最後停留的分支；重新生成或編輯過的訊息可用「‹ 1/2 ›」切換版本，附件可預覽與下載。',
          side: 'left',
        },
        options.audit && menuStep,
      ]
    : [
        {
          target: 'nx-admin-user-inspector .inspector-subtitle',
          title: '這位使用者的概況',
          description:
            '角色、登入方式與最近活動；所屬單位在「帳號」，最近 30 天的生成、tokens 與附件用量在「用量」。',
          side: 'bottom',
        },
        menuStep,
        {
          target: 'nx-admin-user-inspector .admin-inspector nx-tabs',
          title: '依頁籤管理',
          description:
            '每個頁籤各自儲存。\n切換頁籤、換人或關閉時，未儲存的變更會先確認。\n「對話」頁籤可唯讀查看他的對話。',
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

const IMPORT = 'nx-directory-import-dialog';

/**
 * 從 AD 匯入對話框的導覽，依步驟（選取、預覽、結果）介紹畫面上的區塊；
 * 「仍要匯入 AD 已停用的帳號」只在預覽且有停用的人時出現，沒有就由導覽略過。
 */
export function directoryImportTour(options: {
  step: 'select' | 'preview' | 'result';
}): ProductTourDefinition {
  const steps: TourSteps =
    options.step === 'select'
      ? [
          {
            target: `${IMPORT} .directory-tree nx-tree-view`,
            title: '依 OU 瀏覽',
            description:
              '和 AD 工具一樣逐層展開；數字是含下層的人數，淡色節點沒有人員，藍色小計是這裡已選的人數。',
            side: 'right',
          },
          {
            target: `${IMPORT} .directory-subtree`,
            title: '包含子 OU',
            description: '預設只列節點直屬的人；打開後連同下層 OU 一起列出（一次最多 1,000 人）。',
            side: 'top',
          },
          {
            target: `${IMPORT} .directory-toolbar nx-view-switch`,
            title: '依平台狀態篩選',
            description:
              '未匯入、有異動（AD 與平台不同）、已同步；也可在節點內搜尋或隱藏 AD 停用的人。',
            side: 'bottom',
          },
          {
            target: `${IMPORT} .directory-people`,
            title: '勾選要匯入的人',
            description:
              '勾選框才是選取。\n表頭可全選目前清單；Shift＋勾選可連續選一段。\n帳號衝突、帳號名不支援與已刪除的人不能勾選。',
          },
          {
            target: `${IMPORT} .directory-side`,
            title: '匯入籃與 AD 屬性',
            description:
              '切換節點時，已選的人會留在匯入籃，依新增、更新、略過分組。\n點清單中的人，這裡會列出完整的 AD 屬性（可篩選）與會改的欄位。',
            side: 'left',
          },
          {
            target: `${IMPORT} .directory-preview`,
            title: '先預覽',
            description: '伺服器依 AD 重新比對，逐人列出會新建、綁定、更新或略過；確認前不寫入。',
            side: 'top',
          },
        ]
      : [
          {
            target: `${IMPORT} .directory-summary`,
            title: options.step === 'preview' ? '預覽摘要' : '匯入結果',
            description:
              options.step === 'preview'
                ? '會新增、更新與略過的人數；確認匯入只寫入新增與更新的人。'
                : '已寫入的人數；標示「未完成」的人在預覽後資料有變動，「返回選取」重新預覽即可。',
            side: 'bottom',
          },
          options.step === 'preview' && {
            target: `${IMPORT} .directory-include-disabled`,
            title: 'AD 已停用的帳號',
            description:
              '預設不匯入；到職前先建好、尚未啟用的帳號可勾選匯入，AD 啟用前仍無法以 AD 登入。',
            side: 'bottom',
          },
          {
            target: `${IMPORT} nx-directory-outcome-table`,
            title: '逐人結果',
            description: '每人的結果與會改的欄位（前 → 後），需要你另外處理的情況會附註說明。',
          },
          options.step === 'preview' && {
            target: `${IMPORT} .directory-confirm`,
            title: '確認匯入',
            description:
              '新建的帳號與首次 AD 登入相同：一般使用者角色、只開 AD 登入；角色與政策之後在使用者詳情調整。',
            side: 'top',
          },
        ];
  return { steps };
}
