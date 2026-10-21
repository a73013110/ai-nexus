import type { ProductTourDefinition } from '@shared/ui/product-tour';

/**
 * 資料來源頁的導覽。頁面只唯讀查詢外部來源，沒有新增、修改或同步來源的入口；
 * 目前來源尚未可用時改說明原因，帶入對話與儲存快照依對應功能授權顯示。
 * 結果、本文與歷程只在查詢或開啟紀錄後才在畫面上，找不到時引擎自動略過。
 */
export function integrationsTour(options: {
  /** 目前選取的來源已設定連線且有權查詢。 */
  queryable: boolean;
  /** 可使用聊天，紀錄上有「帶入對話」。 */
  chat: boolean;
  /** 可使用成果，紀錄上有「儲存個人快照」。 */
  artifacts: boolean;
  /** 聊天頁的顯示名稱。 */
  chatName: string;
  /** 成果頁的顯示名稱。 */
  artifactsName: string;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-integrations-page .feature-title',
        title: '唯讀查詢公文與校務資料',
        description:
          '以你在原系統的來源權限查詢公文與校務資料，接續到對話或成果；這裡不會修改來源資料，也沒有簽核入口。',
        side: 'bottom',
      },
      {
        target: 'nx-integrations-page .source-catalog',
        title: '選擇來源',
        description:
          '每張卡片是一個外部系統，顯示連線是否已設定；尚未設定連線或沒有來源授權時以鎖頭標示並說明原因。',
        side: 'bottom',
      },
      !options.queryable && {
        target: 'nx-integrations-page .source-unavailable',
        title: '來源尚未可用',
        description:
          '來源需要管理員完成連線設定，並把你所在的群組加入來源授權；只有功能權限還不能查詢。',
      },
      options.queryable && {
        target: 'nx-integrations-page .source-search',
        title: '搜尋標題或識別碼',
        description:
          '輸入兩個字以上，可依來源資料類型篩選；結果只包含你在來源端有權閱讀的資料，依更新時間排序。',
        side: 'bottom',
      },
      options.queryable && {
        target: 'nx-integrations-page .source-results',
        title: '開啟一筆紀錄',
        description: '點選結果讀取本文、狀態與版本；每次讀取都會向來源重新確認授權。',
        side: 'right',
      },
      options.queryable && {
        target: 'nx-integrations-page .source-reading .resource-heading',
        title: '閱讀最新版本',
        description:
          '開著的紀錄在分頁可見時每 30 秒重新檢查一次，也可用重新載入鈕立即取得最新資料與授權。',
        side: 'bottom',
      },
      options.queryable && {
        target: 'nx-integrations-page .source-history',
        title: '歷程與版本',
        description: '列出來源提供的狀態與版本歷程，最多顯示最近 100 筆，完整歷程請至原系統檢視。',
      },
      options.queryable &&
        options.chat && {
          target: 'nx-integrations-page .source-chat',
          title: '帶入對話',
          description: `建立一個個人對話並放入草稿，到「${options.chatName}」檢視後再送出；內容過長不會截斷，請分段使用。`,
          side: 'bottom',
        },
      options.queryable &&
        options.artifacts && {
          target: 'nx-integrations-page .source-snapshot',
          title: '儲存個人快照',
          description: `再確認一次授權與版本後，把當下文字存到「${options.artifactsName}」；之後來源更新或撤權不會回收已儲存的快照。`,
          side: 'bottom',
        },
    ],
  };
}
