import type { ProductTourDefinition } from '@shared/ui/product-tour';

/** 閱讀器的檢視方式：PDF／圖片看原樣、Markdown／HTML 看預覽、其他直接看文字。 */
export type ReaderView = 'original' | 'rich' | 'text';

/**
 * 文件閱讀器（獨立頁面與預覽對話框共用）的導覽；工具列的步驟依文件類型而定。
 * 沒有原檔時下載鈕不在畫面上，該步驟會自動略過。
 */
export function documentReaderTour(options: {
  view: ReaderView;
  /** 原檔是圖片（文案說辨識文字，而非擷取文字）。 */
  image: boolean;
  /** 有多頁可切換。 */
  paged: boolean;
  /** 有可搜尋的擷取文字。 */
  searchable: boolean;
  /** 文字辨識或索引尚未完成（或已失敗），檢視器的動態島顯示進度。 */
  processing: boolean;
  /** 嵌在預覽對話框，而非獨立頁面。 */
  embedded: boolean;
}): ProductTourDefinition {
  const original = options.view === 'original';
  return {
    steps: [
      {
        target: 'nx-document-viewer .dialog-title',
        title: '核對來源原文',
        description:
          '標題列顯示檔名、格式與頁數。\n從回答的引用或檢索結果開啟時，會直接停在被引用的那一頁。',
        side: 'bottom',
      },
      original && {
        target: 'nx-document-viewer .viewer-body nx-view-switch',
        title: options.image ? '原始圖片與辨識文字' : '原始頁面與擷取文字',
        description: options.image
          ? '原始圖片顯示上傳的檔案；辨識文字列出 AI 從圖片讀出的文字，請與原圖核對。'
          : '・原始頁面：顯示 PDF 原樣。\n・擷取文字：檢索與引用實際讀到的逐頁文字；AI 辨識出的會標示「請核對原文」。',
        side: 'bottom',
      },
      options.view === 'rich' && {
        target: 'nx-document-viewer .viewer-body nx-view-switch',
        title: '預覽與原始碼',
        description:
          'Markdown 與 HTML 可在預覽、原始碼與並排之間切換（並排需要較寬的畫面）；含指令碼的 HTML 預設不執行，確認後才在隔離環境執行。',
        side: 'bottom',
      },
      options.paged && {
        target: 'nx-document-viewer .reader-pagination',
        title: '切換頁面',
        description: '用前後鈕或頁碼選單跳到指定頁；擷取文字也逐頁對應原始頁面。',
        side: 'bottom',
      },
      original && {
        target: 'nx-document-viewer .viewer-zoom',
        title: '縮放',
        description: options.image
          ? '圖片預設符合視窗，可放大檢查細節。'
          : '調整頁面縮放比例，檢查小字或表格。',
        side: 'bottom',
      },
      options.searchable && {
        target: 'nx-document-viewer .viewer-find',
        title: '搜尋內文',
        description: `按搜尋鈕或 Ctrl+F 搜尋文字，列出符合的頁，點選即跳到該頁並標示關鍵字。${
          options.view === 'rich' ? 'HTML 預覽時要切到原始碼才能搜尋。' : ''
        }`,
        side: 'bottom',
      },
      options.processing && {
        target: 'nx-document-viewer .viewer-status',
        title: '處理進度',
        description:
          '文字辨識與索引在背景進行，進度顯示在這裡。\n完成後，文字自動出現。\n失敗時，有權限者可就地重新處理。',
        side: 'bottom',
      },
      {
        target: 'nx-document-viewer .viewer-download',
        title: '下載原檔',
        description: '需要原始格式時，直接下載上傳的檔案。',
        side: 'bottom',
      },
      options.embedded && {
        target: 'nx-document-viewer .viewer-new-tab',
        title: '放大或另開分頁',
        description: '可放大預覽視窗，或在新分頁開啟同一頁，方便與對話並排對照。',
        side: 'bottom',
      },
      !options.embedded && {
        target: 'nx-document-reader .reader-return',
        title: '回到來源頁',
        description: '回到開啟文件前的頁面；直接開啟網址時回到所屬的檔案庫、知識庫或分享頁。',
        side: 'bottom',
      },
    ],
  };
}
