import type { ProductTourDefinition } from '@shared/ui/product-tour';

/**
 * 平台用量頁的導覽。頁面本身不算金額，費用步驟只在檢視者能進總覽時指向那裡；
 * 區間內還沒有任何生成時，略過回報完整度與使用來源，避免描述不存在的數字。
 */
export function usageTour(options: {
  /** 區間內沒有任何生成請求（已讀取完成）。 */
  empty: boolean;
  /** 檢視者可進總覽時的功能名稱；`null` 不提費用去處。 */
  dashboard: string | null;
  /** 檢視者可管理價格時的功能名稱；`null` 時以「價格表」稱呼。 */
  prices: string | null;
}): ProductTourDefinition {
  const prices = options.prices ? `「${options.prices}」` : '價格表';
  return {
    steps: [
      {
        target: 'nx-usage-page .feature-title',
        title: '看全平台的 AI 用量',
        description:
          '彙整所有使用者的生成次數、tokens 與儲存空間，並檢查模型供應商與網路搜尋是否可用；只能讀取，不會變更設定。',
        side: 'bottom',
      },
      {
        target: 'nx-usage-page .usage-period',
        title: '固定最近 30 天',
        description:
          '含今天共 30 天，以 UTC 日界切分，涵蓋全平台、不分部門，不能自訂期間；右側按鈕重新讀取最新數字。',
        side: 'bottom',
      },
      {
        target: 'nx-usage-page nx-stat-grid',
        title: '四個摘要數字',
        description:
          '啟用帳號下方是區間內實際用過 AI 的人數；生成次數拆成完成、失敗與取消；原檔容量不受期間限制，同一份檔案被多處引用只計一次。',
        side: 'bottom',
      },
      !options.empty && {
        target: 'nx-usage-page .usage-coverage',
        title: 'tokens 只計模型回報',
        description: `tokens 只加總模型實際回報的數字，未完整回報的請求不估算、也不當成零，次數另列於此。${
          options.dashboard
            ? `此頁不算金額；費用依${prices}在呼叫當時的價格計算，到「${options.dashboard}」切換「整個平台」查看與匯出 CSV。`
            : ''
        }`,
      },
      {
        target: 'nx-usage-page .usage-models',
        title: '各模型使用狀況',
        description: options.empty
          ? '區間內還沒有模型使用紀錄；有生成後會依請求數列出每個模型的失敗率、tokens 與處理時間。'
          : '依請求數由多到少排列，附失敗率、輸入與輸出 tokens、處理時間與未完整回報次數，用來找出常用或常失敗的模型。',
      },
      !options.empty && {
        target: 'nx-usage-page .usage-kinds',
        title: '用量從哪裡來',
        description:
          '把請求依對話、文字處理、評測、圖片辨識、知識向量與網路搜尋等來源分類，占比以請求數計算。',
      },
      {
        target: 'nx-usage-page .usage-services',
        title: '服務狀態',
        description:
          '模型供應商顯示能否讀取模型目錄，網路搜尋顯示是否已設定；無法連線時看說明欄的原因。',
      },
    ],
  };
}
