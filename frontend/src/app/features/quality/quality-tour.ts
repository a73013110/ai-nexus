import type { ProductTourDefinition, TourStep } from '@shared/ui/product-tour';

/**
 * The collection tour. The card step follows the open tab (評測集 or 我的回饋) and is skipped while
 * 該分頁沒有列出任何東西；回饋卡只對有聊天功能的檢視者導回對話。
 */
export function qualityTour(options: { feedback: boolean; chat: boolean }): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-quality-page .feature-title',
        title: '用固定題目衡量品質',
        description:
          '把代表問題、參考答案與檢核詞存成評測集，用同一批題目比較模型或指令，留下可追溯的結果。',
        side: 'bottom',
      },
      {
        target: 'nx-quality-page nx-resource-gallery nx-view-switch',
        title: '評測集與我的回饋',
        description: '「我的回饋」列出你在 AI 回覆下方標示為有幫助或待改善的回答，只有你看得到。',
        side: 'bottom',
      },
      {
        target: 'nx-quality-page .gallery-search',
        title: options.feedback ? '用對話名稱尋找' : '用名稱尋找評測集',
        description: '輸入名稱的任一段，清單會立即篩選。',
        side: 'bottom',
      },
      options.feedback
        ? {
            target: 'nx-quality-page .resource-card',
            title: '回饋過的回答',
            description: `卡片顯示你的評價與補充說明${options.chat ? '；點選回到原對話查看該則回答' : ''}。`,
          }
        : {
            target: 'nx-quality-page .resource-card',
            title: '打開評測集',
            description: '卡片顯示題數、題庫版本與你的權限；點選查看比較紀錄與逐題答案。',
          },
      {
        target: 'nx-quality-page .quality-create',
        title: '建立評測集',
        description: '寫下 1–20 題問題、參考答案、必要詞與禁用詞，儲存後即可開始比較。',
        side: 'bottom',
      },
      {
        target: 'nx-quality-page .quality-list-menu',
        title: '匯入題庫與檢索評測',
        description:
          '「評測操作」可匯入先前匯出的題庫 JSON，或前往檢索評測比較知識庫的四種檢索模式。',
        side: 'bottom',
      },
    ],
  };
}

/**
 * 評估集導覽。有哪些步驟取決於檢視者對此集合的權限，以及畫面上的執行；
 * 沒有顯示結果時，會略過答案與審查按鈕。
 */
export function evaluationSetTour(options: {
  /** 檢視者可以編輯案例。 */
  edit: boolean;
  /** 檢視者擁有此集合（可存取並刪除）。 */
  owner: boolean;
  /** 畫面上的執行接受檢視者的手動審查。 */
  review: boolean;
}): ProductTourDefinition {
  const steps: TourStep[] = [
    {
      target: 'nx-quality-page .feature-title',
      title: '評測集的內容',
      description: '頁首列出說明、題數與題庫版本；每次儲存題庫都會升級版本，舊比較保留當時的題目。',
      side: 'bottom',
    },
    {
      target: 'nx-quality-page .quality-run',
      title: '開始比較',
      description:
        '設定 1–3 個方案的指令（允許時可選模型），每題每個方案各呼叫一次模型並計入你的用量。',
      side: 'bottom',
    },
    {
      target: 'nx-quality-page .quality-runs',
      title: '比較紀錄',
      description:
        '在背景逐題執行，離開頁面不會停止；可切換過去的紀錄，發起人能停止或接續重試，已完成的答案不會重算。',
    },
    {
      target: 'nx-quality-page .evaluation-case',
      title: '逐題比較答案',
      description:
        '各方案並排顯示回答、耗時、tokens 與必要詞、禁用詞的命中數；這些是文字檢核線索，不是語意正確率。',
    },
    ...(options.review
      ? [
          {
            target: 'nx-quality-page .evaluation-review',
            title: '補上人工評分',
            description: '為答案打 1–5 分並寫下判讀說明，與自動指標分開保存，授權成員都看得到。',
          },
        ]
      : []),
    {
      target: 'nx-quality-page .quality-set-menu',
      title: '評測集操作',
      description: `「評測集操作」可${options.edit ? '編輯題庫、' : ''}匯出題庫 JSON${options.owner ? '、設定存取權限或刪除評測集' : ''}。`,
      side: 'bottom',
    },
  ];
  return { steps };
}

/** 檢索評估導覽；在第一次評估存在之前，會略過報告步驟。 */
export const RETRIEVAL_TOUR: ProductTourDefinition = {
  steps: [
    {
      target: 'nx-retrieval-evaluation .feature-title',
      title: '比較四種檢索模式',
      description: '用同一批標註題目依序跑向量、全文、混合與混合重排，找出最能召回正確文件的方式。',
      side: 'bottom',
    },
    {
      target: 'nx-retrieval-evaluation .retrieval-reports',
      title: '閱讀評測報告',
      description:
        '每種模式列出 Recall、MRR、nDCG、無來源拒答率與各階段延遲；含降級或略過的組別會標示，請先確認再比較。可切換紀錄或下載 JSON 報告。',
    },
    {
      target: 'nx-retrieval-evaluation .retrieval-collections',
      title: '選擇知識庫',
      description: '最多選三個你可存取的知識庫；標註的相關文件必須已完成處理且在選取範圍內。',
    },
    {
      target: 'nx-retrieval-evaluation .retrieval-corpus',
      title: '貼上驗收集',
      description:
        '每題標註相關文件、頁碼與 1–3 級相關性，無答案題用來檢查是否不提供來源；也可用面板右上的上傳圖示匯入 JSON。',
    },
    {
      target: 'nx-retrieval-evaluation .retrieval-start',
      title: '開始四模式評測',
      description: '在背景執行，一次只能進行一個；報告只有你看得到，也不會生成回答。',
      side: 'top',
    },
  ],
};
