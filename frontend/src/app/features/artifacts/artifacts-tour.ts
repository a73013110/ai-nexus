import type { ProductTourDefinition, TourStep } from '@shared/ui/product-tour';

/**
 * The collection tour. The opening step points to the chat's "儲存成果" only for viewers with chat;
 * the card step is skipped while no document is listed.
 */
export function artifactsTour(options: { chat: boolean }): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-artifacts-page .feature-title',
        title: '把回答整理成文件',
        description: `成果文件是可交付的工作文件，每次儲存都保留一個不可變的版本。${options.chat ? '在對話中按回答旁的「儲存成果」，或選取文字後另存，也會出現在這裡。' : ''}`,
        side: 'bottom',
      },
      {
        target: 'nx-artifacts-page nx-resource-gallery nx-view-switch',
        title: '我擁有與共用給我',
        description: '在全部、你擁有的與同事共用給你的成果之間切換。',
        side: 'bottom',
      },
      {
        target: 'nx-artifacts-page .gallery-search',
        title: '用名稱尋找成果',
        description: '輸入名稱的任一段，清單會立即篩選。',
        side: 'bottom',
      },
      {
        target: 'nx-artifacts-page .resource-card',
        title: '打開成果',
        description: '卡片顯示目前版本、你的權限與最後更新時間；點選進入成果頁閱讀、編輯或匯出。',
      },
      {
        target: 'nx-artifacts-page .artifact-create',
        title: '新增成果',
        description: '填寫名稱並撰寫 Markdown 內容，儲存後直接開啟成果頁。',
        side: 'bottom',
      },
    ],
  };
}

/**
 * The document tour. Which steps exist follows the viewer's rights and whether the editor is open,
 * and the version picker shows only once a second version exists.
 */
export function artifactTour(options: {
  /** The viewer may edit the version on screen. */
  editable: boolean;
  /** The editor is open (編輯 or 並排預覽). */
  editing: boolean;
  /** The owner's "成果操作" menu is shown. */
  manage: boolean;
  /** Read-only links can be created. */
  shared: boolean;
}): ProductTourDefinition {
  const steps: TourStep[] = [
    {
      target: 'nx-artifacts-page .feature-title',
      title: '成果的狀態',
      description: '標題下方顯示你的權限、正在檢視的版本，以及是否有尚未儲存的修改。',
      side: 'bottom',
    },
    {
      target: 'nx-artifacts-page .artifact-version',
      title: '切換版本',
      description:
        '每次儲存都建立新版本，舊版本保持不變；檢視舊版本時可「將此版帶入編輯」，儲存後成為最新版本。',
      side: 'bottom',
    },
    options.editable
      ? {
          target: 'nx-artifacts-page .artifact-toolbar nx-view-switch',
          title: '閱讀、編輯或並排預覽',
          description: options.editing
            ? '在閱讀、編輯與並排預覽之間切換；並排預覽一邊修改 Markdown、一邊看排版結果。'
            : '切到「編輯」或「並排預覽」即可修改；在編輯區選取文字，可交給 AI 改寫、摘要、解釋或翻譯。',
          side: 'bottom',
        }
      : {
          target: 'nx-artifacts-page .artifact-toolbar nx-view-switch',
          title: '閱讀成果',
          description: '你目前只能閱讀這個版本；具編輯權限的成員在最新版本可切換到編輯或並排預覽。',
          side: 'bottom',
        },
    ...(options.editing
      ? [
          {
            target: 'nx-artifacts-page .artifact-editor',
            title: '選取段落交給 AI',
            description:
              '選取文字後上方出現改寫、摘要、解釋與翻譯，單次上限 8,000 字元；確認結果後才會替換原段落。',
          },
          {
            target: 'nx-artifacts-page .artifact-save',
            title: '儲存新版本',
            description:
              'Ctrl / ⌘ S 也能儲存。若同事先存了較新的版本，你的文字會保留在畫面上供合併。',
            side: 'bottom' as const,
          },
        ]
      : []),
    {
      target: 'nx-artifacts-page .artifact-export',
      title: '匯出已儲存的版本',
      description: '匯出目前檢視的版本為 Word、PDF 或 Markdown；尚未儲存的修改不會包含在內。',
      side: 'bottom',
    },
    ...(options.manage
      ? [
          {
            target: 'nx-artifacts-page .artifact-menu',
            title: '分享與權限',
            description: `「成果操作」可${options.shared ? '建立唯讀分享、' : ''}設定具名的讀者、編輯者或唯讀群組，或移除成果文件與所有版本。`,
            side: 'bottom' as const,
          },
        ]
      : []),
  ];
  return { steps };
}
