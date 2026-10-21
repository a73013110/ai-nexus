import type { ProductTourDefinition, TourSteps } from '@shared/ui/product-tour';

/** 目前分頁之外的狀態；各分頁的步驟只指向畫面上那一個分頁的內容。 */
export interface SettingsTourOptions {
  /** `SETTINGS_TABS` 的 id。 */
  tab: string;
  /** 儲存列顯示中（可儲存的分頁，或別的分頁還有未儲存的變更）。 */
  saveBar: boolean;
  /** 正以測試身分檢視，帳號分頁的變更按鈕都不顯示。 */
  testing: boolean;
}

const host = 'nx-settings-page';

function tabSteps(options: SettingsTourOptions): TourSteps {
  switch (options.tab) {
    case 'appearance':
      return [
        {
          target: `${host} nx-appearance-tab .appearance-workspace`,
          title: '主題與動態效果',
          description: '主題可跟隨作業系統；減少動態效果會保留狀態提示，只省掉進場與持續動畫。',
          side: 'bottom',
        },
        {
          target: `${host} nx-appearance-tab .appearance-reading`,
          title: '調整閱讀版面',
          description:
            '字級、行距、內容寬度與訊息密度只影響對話與文件正文，一般介面維持固定字級；側欄寬度讓長對話名稱好讀。',
        },
        {
          target: `${host} nx-appearance-tab .reading-preview`,
          title: '即時預覽',
          description: '改動會立刻套用在整個工作區與這段範例上，不必儲存就能比較效果。',
        },
      ];
    case 'conversation':
      return [
        {
          target: `${host} nx-conversation-tab .conversation-input`,
          title: '送出與跟隨',
          description:
            '關閉「按 Enter 送出」後改用 Ctrl／⌘ + Enter 送出；自動跟隨在你往上閱讀時暫停，不會把畫面拉走。',
          side: 'bottom',
        },
        {
          target: `${host} nx-conversation-tab .conversation-defaults`,
          title: '新對話的預設',
          description:
            '預設模型只列出管理員允許你使用的模型，政策不開放選擇時由系統指定；模型不支援所選思考強度時改用模型預設。',
        },
        {
          target: `${host} nx-conversation-tab .conversation-web-search`,
          title: '網路搜尋是否可用',
          description:
            '啟用時預設關閉，在輸入區按「搜尋網路」才把該次提問送往搜尋服務；未啟用時輸入區不顯示按鈕，原因寫在這裡。',
        },
      ];
    case 'notifications':
      return [
        {
          target: `${host} nx-notifications-tab .notification-completion`,
          title: '回覆完成通知',
          description:
            '要先允許這個瀏覽器的通知才能開啟；工作區在背景時提醒回覆已完成，通知不含提問或回覆內容。',
          side: 'bottom',
        },
      ];
    case 'data':
      return [
        {
          target: `${host} nx-data-tab .data-autosave`,
          title: '自動保存草稿',
          description:
            '未送出的文字與附件參照依帳號存在這個瀏覽器，不同步到其他裝置；共用電腦建議關閉。',
          side: 'bottom',
        },
        {
          target: `${host} nx-data-tab .data-clear`,
          title: '清除本機草稿',
          description: '確認後移除這個瀏覽器中你所有未送出的草稿，無法復原；已送出的對話不受影響。',
        },
      ];
    case 'account':
      return [
        {
          target: `${host} nx-account-tab nx-account-profile`,
          title: '個人資料與通知信箱',
          description: options.testing
            ? '姓名與帳號由公司目錄提供；測試身分期間只能查看，不能變更信箱。'
            : '姓名與帳號由公司目錄提供；通知信箱用來收驗證碼與安全通知，AD 提供的由目錄維護，其他可自行設定並以驗證碼確認。',
          side: 'bottom',
        },
        {
          target: `${host} nx-account-tab nx-account-sign-in`,
          title: '登入方式',
          description: options.testing
            ? '顯示目前的登入方式與本地登入狀態；要返回管理者請使用登入者選單。'
            : '顯示這次的登入方式；系統開放時可另設本地帳號與密碼當備援。變更前可能要先確認身分，完成後其他裝置的登入會登出。',
        },
        {
          target: `${host} nx-account-tab nx-account-access`,
          title: '角色與可用功能',
          description: '依你自己的側欄分組列出角色開放的功能，只供查看；需要更多權限請聯絡管理員。',
        },
      ];
    case 'usage':
      return [
        {
          target: `${host} nx-usage-tab .usage-budget`,
          title: '今日 token 預算',
          description: '各模型今天還能用多少，輸入與輸出合計，進行中的回覆會先預留；每天重設。',
          side: 'bottom',
        },
        {
          target: `${host} nx-usage-tab nx-stat-grid`,
          title: '最近 30 天用量',
          description: '生成次數、平均耗時與 tokens；tokens 只計模型有回報的紀錄，不估算。',
          side: 'bottom',
        },
        {
          target: `${host} nx-usage-tab .usage-storage`,
          title: '原檔容量',
          description: '草稿也計入，同一原檔被多處引用只計一次；群組有給附件空間上限時一併列出。',
        },
      ];
    case 'shortcuts':
      return [
        {
          target: `${host} nx-shortcuts-tab nx-setting-group`,
          title: '常用按鍵',
          description:
            '清單與實際綁定的按鍵同一份，依作業系統顯示 ⌘ 或 Ctrl；送出與換行跟著「對話」分頁的 Enter 設定。',
          side: 'bottom',
        },
      ];
    default:
      return [];
  }
}

/**
 * 個人設定的導覽；開頭說明分類切換，接著只介紹目前分頁，有儲存列時最後說明預覽與儲存。
 * 帳號分頁在測試身分期間改說明唯讀。
 */
export function settingsTour(options: SettingsTourOptions): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${host} .settings-sidebar`,
        title: '切換設定分類',
        description:
          '從左側切換分類，也可用關鍵字（例如「深色」、「Enter」）搜尋；導覽只介紹目前分類，換分類後可再開一次。',
        side: 'right',
      },
      ...tabSteps(options),
      options.saveBar && {
        target: `${host} .settings-savebar`,
        title: '預覽後再儲存',
        description:
          '修改立即預覽，按「儲存變更」才寫回帳號，其他裝置也會套用；「還原」恢復已儲存的值，未儲存就關閉會先確認。',
      },
    ],
  };
}
