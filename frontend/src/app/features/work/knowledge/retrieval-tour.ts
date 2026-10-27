import type { ProductTourDefinition } from '@shared/ui/product-tour';

/** 檢索管理的導覽。啟用與清除只出現在對應狀態的索引列，沒有這種索引時自動略過。 */
export function retrievalTour(): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-retrieval-page .feature-title',
        title: 'AI 回答的依據從這裡找',
        description:
          '知識庫檢索只用「使用中」索引；這頁管理索引、檢查檢索依賴的服務，並可直接試查結果。',
        side: 'bottom',
      },
      {
        target: 'nx-retrieval-page .retrieval-profile',
        title: '檢索索引與覆蓋率',
        description:
          '每個向量模型與維度各一個索引；覆蓋率是已向量化的片段比例，下方列出片段數、待處理文件與重建進度。',
      },
      {
        target: 'nx-retrieval-page .retrieval-rebuild',
        title: '重建索引',
        description: '在背景重新向量化知識片段，會呼叫向量模型並計入用量；確認後才開始。',
        side: 'left',
      },
      {
        target: 'nx-retrieval-page .retrieval-activate',
        title: '啟用新索引',
        description:
          '換向量模型或維度會建立新索引，覆蓋率達 100% 且文件全部就緒才能啟用；原本的使用中索引隨之退役。',
        side: 'left',
      },
      {
        target: 'nx-retrieval-page .retrieval-clear',
        title: '清除退役向量',
        description:
          '退役索引的向量保留到清除為止；清除前會確認，原始檔、逐頁文字與索引紀錄都保留。',
        side: 'left',
      },
      {
        target: 'nx-retrieval-page .retrieval-capabilities',
        title: '服務能力',
        description:
          '檢查 SQL 原生向量、全文檢索與繁體中文斷詞器、全文索引，以及向量與重排模型；缺少的項目會讓對應的檢索方式無法使用。',
      },
      {
        target: 'nx-retrieval-page .retrieval-probe',
        title: '驗證檢索模型',
        description:
          '模型在驗證前顯示「未驗證」；驗證只傳送合成資料實際呼叫向量與重排模型，會計入模型呼叫配額。',
        side: 'bottom',
      },
      {
        target: 'nx-retrieval-page .retrieval-bench',
        title: '檢索測試台',
        description:
          '選最多三個你有權限的知識庫、檢索模式與是否重排，輸入問題即可用使用中索引試查。',
      },
      {
        target: 'nx-retrieval-page .retrieval-result',
        title: '命中結果',
        description:
          '列出各階段耗時，每筆附頁碼、向量與全文排名、RRF 與重排分數；點選在閱讀器開啟並停在該頁。',
      },
    ],
  };
}
