import type { ProductTourDefinition, TourStep } from '@shared/ui/product-tour';

/**
 * The task list tour. The card steps follow what the current filter shows: the result link and
 * stop / retry steps appear only when some card offers them.
 */
export function jobsTour(options: {
  /** At least one task card is listed. */
  cards: boolean;
  /** A listed card links to the document or review it produced. */
  results: boolean;
  /** A listed card can be stopped or retried. */
  controls: boolean;
}): ProductTourDefinition {
  const steps: TourStep[] = [
    {
      target: 'nx-jobs-page .feature-title',
      title: '耗時的處理交給背景',
      description:
        '文件辨識、索引、評測與程式碼 review 都在伺服器上處理，離開頁面或關閉瀏覽器也不會中斷；結束時通知中心會告訴你。',
      side: 'bottom',
    },
    {
      target: 'nx-jobs-page nx-resource-gallery nx-view-switch',
      title: '依狀態篩選',
      description:
        '「處理中」列出排隊與執行中的任務；「需要處理」集中失敗或已停止的任務，方便重試。',
      side: 'bottom',
    },
    {
      target: 'nx-jobs-page .gallery-search',
      title: '用名稱尋找任務',
      description: '輸入文件或任務名稱的任一段，清單會立即篩選。',
      side: 'bottom',
    },
    ...(options.cards
      ? [
          {
            target: 'nx-jobs-page .job-card',
            title: '看真實進度',
            description:
              '卡片顯示種類、目前階段與完成數量，每 3 秒自動更新；還不知道總量時只顯示處理中，不估算百分比。',
          },
        ]
      : []),
    ...(options.results
      ? [
          {
            target: 'nx-jobs-page .job-result',
            title: '查看處理結果',
            description: '直接開啟任務處理的文件或程式碼 review 報告，核對結果。',
          },
        ]
      : []),
    ...(options.controls
      ? [
          {
            target: 'nx-jobs-page .job-control',
            title: '停止或重試',
            description:
              '處理中的任務可要求停止，會在目前步驟結束後停下；失敗或已停止的任務可重試，最多處理六次。',
          },
        ]
      : []),
  ];
  return { steps };
}
