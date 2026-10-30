import type { ProductTourDefinition, TourSteps } from '@shared/ui/product-tour';

/** 集合導覽；沒有列出任何集合時，卡片步驟會略過。 */
export const KNOWLEDGE_TOUR: ProductTourDefinition = {
  steps: [
    {
      target: 'nx-knowledge-page .feature-title',
      title: '可查核的資料來源',
      description:
        '知識庫保存文件的頁面與索引。\n在對話選取後，AI 會先檢索這些來源，回答附上依據與頁碼。',
      side: 'bottom',
    },
    {
      target: 'nx-knowledge-page nx-resource-gallery nx-view-switch',
      title: '依擁有者篩選',
      description: '在全部、我擁有與同事共用給你的知識庫之間切換。',
      side: 'bottom',
    },
    {
      target: 'nx-knowledge-page .gallery-search',
      title: '用名稱尋找知識庫',
      description: '輸入名稱的任一段，清單會立即篩選。',
      side: 'bottom',
    },
    {
      target: 'nx-knowledge-page .resource-card',
      title: '打開知識庫',
      description: '卡片顯示可查詢的文件數與你的權限；點選進入，管理來源與測試檢索。',
    },
    {
      target: 'nx-knowledge-page .knowledge-create',
      title: '建立知識庫',
      description: '填寫名稱與說明即可建立，新知識庫只有你能存取，之後再放入文件與授權同事。',
      side: 'bottom',
    },
  ],
};

/**
 * 已開啟集合的導覽。新增來源與集合選單依檢視者的權限而定，
 * 不在畫面上的步驟（空列表的文件、隱藏的篩選）會略過。
 */
export function collectionTour(options: { edit: boolean; manage: boolean }): ProductTourDefinition {
  const steps: TourSteps = [
    {
      target: 'nx-knowledge-page .feature-title',
      title: '這個知識庫的用途',
      description: '標題下方是知識庫的說明，提醒成員這裡收的是哪些資料。',
      side: 'bottom',
    },
    options.edit && {
      target: 'nx-knowledge-page .knowledge-upload',
      title: '上傳文件',
      description:
        '支援 PDF、Word、Excel、PowerPoint、圖片與文字，也可把檔案拖曳到來源區；每批最多十份，掃描 PDF 與圖片會自動辨識文字。',
      side: 'bottom',
    },
    options.edit && {
      target: 'nx-knowledge-page .knowledge-sources .panel-actions',
      title: '貼上文字或從檔案庫加入',
      description: '沒有檔案時可直接貼上純文字建立來源；已在檔案庫的原檔不必重新上傳。',
      side: 'bottom',
    },
    {
      target: 'nx-knowledge-page .document-filter',
      title: '篩選來源文件',
      description: '文件較多時，輸入檔名的任一段即可縮小清單。',
      side: 'bottom',
    },
    {
      target: 'nx-knowledge-page .document-name',
      title: '開啟原檔核對',
      description: '點選文件名稱開啟閱讀，可切換原始頁面與擷取文字、搜尋內文並跳到指定頁碼。',
    },
    {
      target: 'nx-knowledge-page .document-row nx-status-badge',
      title: '處理狀態',
      description:
        '文字辨識與索引在背景進行，完成前顯示「等待處理」或「處理中」。\n只有「已就緒」的文件會被檢索。\n「需要重試」可從文件操作重試索引。',
      side: 'left',
    },
    {
      target: 'nx-knowledge-page .document-row nx-action-menu',
      title: '文件操作',
      description:
        '「⋯」可重新索引或移除文件；貼上建立的來源另可編輯純文字。移除後原檔仍保留在檔案庫。',
      side: 'left',
    },
    {
      target: 'nx-knowledge-page .knowledge-query',
      title: '檢索測試',
      description:
        '輸入問題，檢查會找到哪些段落與頁碼；結果列出檢索模式與分數。\n要讓 AI 引用，請在對話輸入框旁選取知識庫。',
    },
    options.manage && {
      target: 'nx-knowledge-page .page-actions nx-action-menu',
      title: '知識庫操作',
      description: '「⋯」可修改名稱與說明；擁有者另可設定存取權限、移除整個知識庫。',
      side: 'bottom',
    },
  ];
  return { steps };
}
