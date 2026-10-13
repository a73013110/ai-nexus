import type { ProductTourDefinition, TourStep } from '../../shared/ui/product-tour';

const replay = '右上角的問號圖示可隨時重播導覽。';

/** The collection tour; the card step is skipped while no project is listed. */
export const PROJECTS_TOUR: ProductTourDefinition = {
  steps: [
    {
      target: 'nx-projects-page .feature-title',
      title: '把工作放進同一個脈絡',
      description: '專案集中共用指示、參考文件、範本與成果，成員每次提問都從相同的背景開始。',
      side: 'bottom',
    },
    {
      target: 'nx-projects-page nx-resource-gallery nx-view-switch',
      title: '進行中與已封存分開列',
      description: '封存的專案保留文件與歷史，切到「已封存」即可閱讀或還原。',
      side: 'bottom',
    },
    {
      target: 'nx-projects-page .gallery-search',
      title: '用名稱尋找專案',
      description: '輸入名稱的任一段，清單會立即篩選。',
      side: 'bottom',
    },
    {
      target: 'nx-projects-page .resource-card',
      title: '打開專案',
      description: '卡片顯示你的權限與最後更新時間；點選進入專案頁開始工作。',
    },
    {
      target: 'nx-projects-page .project-create',
      title: '建立新專案',
      description: `填寫名稱、工作說明與共用指示即可建立，之後再上傳文件與邀請成員。${replay}`,
      side: 'bottom',
    },
  ],
};

/**
 * The project tour. Which steps exist follows the viewer's features and rights, so the replay hint
 * always lands on the last one; steps still not on screen are skipped when the tour starts.
 */
export function projectTour(options: {
  chat: boolean;
  artifacts: boolean;
  manage: boolean;
}): ProductTourDefinition {
  const steps: TourStep[] = [
    {
      target: 'nx-projects-page .feature-title',
      title: '專案的工作說明',
      description: '標題下方是這個專案的目的，提醒每位成員這裡在做什麼。',
      side: 'bottom',
    },
    ...(options.chat
      ? [
          {
            target: 'nx-projects-page .project-start',
            title: '從專案開始對話',
            description: '新對話自動帶入共用指示與已辨識的參考文件；專案封存後無法開始。',
            side: 'bottom' as const,
          },
        ]
      : []),
    {
      target: 'nx-projects-page .project-templates',
      title: '用範本快速開始',
      description:
        '點選範本會建立專案對話並把提問放進輸入框，確認後再送出。編輯者可新增或修改範本。',
    },
    ...(options.chat
      ? [
          {
            target: 'nx-projects-page .project-conversations',
            title: '只有你看得到的對話',
            description: '這裡列出你在專案中的提問；需要同事閱讀時，在對話建立具名分享。',
          },
        ]
      : []),
    {
      target: 'nx-projects-page .project-guidance',
      title: '共用指示',
      description: '寫下工作目的、回覆格式或應遵守的原則，會套用到每位成員的專案對話。',
    },
    {
      target: 'nx-projects-page .project-files',
      title: '參考文件',
      description:
        '上傳或拖曳文字、PDF 與圖片，辨識完成後隨提問帶入，全文上限 12,000 字元；大量資料請用知識庫。',
    },
    ...(options.artifacts
      ? [
          {
            target: 'nx-projects-page .project-outputs',
            title: '專案成果',
            description: '成果沿用專案成員權限，可檢視版本與匯出。',
          },
        ]
      : []),
    ...(options.manage
      ? [
          {
            target: 'nx-projects-page nx-action-menu',
            title: '管理專案',
            description: '「專案操作」可編輯、封存或還原；擁有者另可設定成員與權限、刪除專案。',
            side: 'bottom' as const,
          },
        ]
      : []),
  ];
  const last = steps.length - 1;
  steps[last] = { ...steps[last], description: `${steps[last].description}${replay}` };
  return { steps };
}
