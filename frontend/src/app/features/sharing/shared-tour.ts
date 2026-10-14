import type { ProductTourDefinition, TourStep } from '../../shared/ui/product-tour';

const replay = '右上角的問號圖示可隨時重播導覽。';

/** Puts the replay hint on whichever step ends up last. */
function withReplay(steps: TourStep[]): ProductTourDefinition {
  const last = steps.length - 1;
  steps[last] = { ...steps[last], description: `${steps[last].description}${replay}` };
  return { steps };
}

/**
 * The collection tour. The opening step names where shares are created only for the features the
 * viewer has; the card step exists only while a share is listed, so the replay hint stays on screen.
 */
export function sharesTour(options: {
  /** Conversations can be shared from the chat's top bar. */
  chat: boolean;
  /** Owned artifacts can be shared from their "成果操作" menu. */
  artifacts: boolean;
  /** At least one share card is on screen. */
  cards: boolean;
}): ProductTourDefinition {
  const places = [
    ...(options.chat ? ['對話右上角'] : []),
    ...(options.artifacts ? ['成果文件的「成果操作」'] : []),
  ];
  return withReplay([
    {
      target: 'nx-shared-page .feature-title',
      title: '收到的分享集中閱讀',
      description: `同事以具名唯讀方式分享給你的對話與成果文件都在這裡，只有指定帳號能開啟。${places.length ? `你也可以在${places.join('或')}建立分享。` : ''}`,
      side: 'bottom',
    },
    {
      target: 'nx-shared-page nx-resource-gallery nx-view-switch',
      title: '分享給我與我分享的',
      description: '切到「我分享的」查看你建立的分享與收件者，開啟後可隨時撤銷。',
      side: 'bottom',
    },
    {
      target: 'nx-shared-page .gallery-search',
      title: '用標題尋找分享',
      description: '輸入標題的任一段，清單會立即篩選。',
      side: 'bottom',
    },
    ...(options.cards
      ? [
          {
            target: 'nx-shared-page .resource-card',
            title: '打開分享',
            description:
              '卡片顯示分享的是對話或成果文件、分享者或收件者與分享時間，已撤銷或到期會標示狀態。',
          },
        ]
      : []),
  ]);
}

/**
 * The open share's tour. The snapshot step follows what was shared, and only the owner learns how
 * to revoke it, so the replay hint always lands on the last step.
 */
export function shareTour(options: {
  /** The share holds an artifact version rather than a conversation branch. */
  artifact: boolean;
  /** The conversation's attachments were shared too. */
  attachments: boolean;
  /** The owner's "分享操作" menu is shown. */
  manage: boolean;
}): ProductTourDefinition {
  return withReplay([
    {
      target: 'nx-shared-page .feature-title',
      title: '分享者與期限',
      description:
        '標題下方顯示分享者與到期時間；頁面會定期重新確認存取權，到期或撤銷時內容立即收起。',
      side: 'bottom',
    },
    options.artifact
      ? {
          target: 'nx-shared-page .share-content',
          title: '分享當下的成果版本',
          description: '這是建立分享時選取的已儲存版本，之後的修改不會出現在這裡。',
        }
      : {
          target: 'nx-shared-page .share-content',
          title: '分享當下的對話',
          description: `這是建立分享時的對話分支快照，之後的新訊息不會出現；知識引用只展開已分享的摘要${options.attachments ? '，附件可直接預覽或下載' : ''}。`,
        },
    {
      target: 'nx-shared-page .share-info',
      title: '分享資訊',
      description: `列出收件者、建立與到期時間${options.artifact ? '及分享的版本' : '，以及附件是否一併分享'}。`,
    },
    ...(options.manage
      ? [
          {
            target: 'nx-shared-page .share-menu',
            title: '撤銷分享',
            description:
              '「分享操作」可撤銷分享，收件者立即無法閱讀或下載附件；原始內容保留，已下載或複製的內容無法收回。',
            side: 'bottom' as const,
          },
        ]
      : []),
  ]);
}
