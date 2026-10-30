import type { ProductTourDefinition } from '@shared/ui/product-tour';

/**
 * 檔案庫導覽。只有具知識庫功能的人，選單步驟才提到「加入知識庫」；
 * 卡片與選單步驟在沒有檔案列出時會略過，而容量步驟會結束導覽，
 * 因為列表載入後頁尾一律會顯示。
 */
export function filesTour(options: { knowledge: boolean }): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-files-page .feature-title',
        title: '你的原檔都在這裡',
        description:
          '對話附件、知識庫與專案上傳的原檔，都會保存到檔案庫。\n同一份檔案可再用於對話或加入知識庫，不必重新上傳。',
        side: 'bottom',
      },
      {
        target: 'nx-files-page .file-upload',
        title: '先保存，再決定用途',
        description: '上傳或直接把檔案拖曳到頁面上；每批最多十份，進度顯示在通知中。',
        side: 'bottom',
      },
      {
        target: 'nx-files-page nx-resource-gallery nx-view-switch',
        title: '依類型篩選',
        description: '在全部、圖片與文件之間切換。',
        side: 'bottom',
      },
      {
        target: 'nx-files-page .file-source',
        title: '依來源篩選',
        description: '只看對話附件、知識庫來源、專案文件，或還沒被任何地方引用的檔案。',
        side: 'bottom',
      },
      {
        target: 'nx-files-page .gallery-search',
        title: '用名稱尋找檔案',
        description: '輸入檔名的任一段，清單會立即篩選。',
        side: 'bottom',
      },
      {
        target: 'nx-files-page .file-card',
        title: '預覽與引用位置',
        description:
          '點選縮圖或名稱開啟預覽，不會離開這一頁。\n下方標籤列出引用這份檔案的對話、知識庫或專案。',
      },
      {
        target: 'nx-files-page .file-menu',
        title: '檔案操作',
        description: `「⋯」可${options.knowledge ? '加入知識庫、' : ''}重新命名、下載原檔或刪除；仍被引用的檔案要先移除引用才能刪除。`,
        side: 'left',
      },
      {
        target: 'nx-files-page .storage-usage',
        title: '留意原檔容量',
        description: '包含草稿，同一份原檔多處引用只計一次；刪除檔案才會釋放容量。',
      },
    ],
  };
}
