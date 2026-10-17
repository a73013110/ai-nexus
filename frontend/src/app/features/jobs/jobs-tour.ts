import type { ProductTourDefinition, TourStep } from '@shared/ui/product-tour';

/**
 * 任務列表的導覽。卡片步驟依目前篩選顯示的內容而定：結果連結與停止／重試步驟，
 * 只有某張卡片提供時才會出現。
 */
export function jobsTour(options: {
  /** 至少列出一張任務卡片。 */
  cards: boolean;
  /** 列出的卡片會連到它產生的文件或審查。 */
  results: boolean;
  /** 列出的卡片可以被停止或重試。 */
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
