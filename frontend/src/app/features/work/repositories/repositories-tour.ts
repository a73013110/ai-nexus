import type { ProductTourDefinition, TourStep, TourSteps } from '@shared/ui/product-tour';
import type { RepositorySection } from './repository-detail';

/** Joins the last two items with 「或」: 「甲、乙或丙」. */
const either = (items: string[]) =>
  items.length > 1 ? `${items.slice(0, -1).join('、')}或${items.at(-1)}` : (items[0] ?? '');

/** 連線選單，每個已連線的檢視者都有；導覽最後都會停在這裡。 */
const connection = (): TourStep => ({
  target: 'nx-repositories-page .repository-account',
  title: '你的 Gitea 連線',
  description:
    '所有內容都以你自己的權杖讀取。「更新授權」換新權杖，「中斷連線」刪除保存的權杖，已匯入的文件與檢閱紀錄會保留。',
  side: 'bottom',
});

/** 清單導覽；沒有東西可顯示時，固定列與卡片步驟會略過。 */
export function repositoriesTour(options: {
  chat: boolean;
  knowledge: boolean;
}): ProductTourDefinition {
  const handoffs = either([
    ...(options.chat ? ['帶入對話'] : []),
    ...(options.knowledge ? ['匯入知識庫'] : []),
    '交給 AI 檢閱',
  ]);
  return {
    steps: [
      {
        target: 'nx-repositories-page .feature-title',
        title: '用你的 Gitea 權限閱讀程式',
        description: `只列出你的 Gitea 帳號能讀的程式庫；閱讀程式碼、提交紀錄、議題與 Pull Request 後，可以${handoffs}。`,
        side: 'bottom',
      },
      {
        target: 'nx-repository-list .pinned',
        title: '釘選的程式庫',
        description: '常用的程式庫在程式庫頁按圖釘，就會固定在列表上方，最多 30 個。',
        side: 'bottom',
      },
      {
        target: 'nx-repository-list .gallery-search',
        title: '搜尋程式庫',
        description: '輸入關鍵字搜尋你能讀取的所有程式庫，不限於已載入的這一頁。',
        side: 'bottom',
      },
      {
        target: 'nx-repository-list .resource-card',
        title: '打開程式庫',
        description:
          '卡片顯示語言、開啟中的 Pull Request 與議題數和更新時間，私人程式庫以鎖頭標示。',
      },
      connection(),
    ],
  };
}

/** 已開啟區段自己的步驟；位於共用標題步驟與選單之間。 */
function sectionSteps(options: {
  section: RepositorySection;
  item: boolean;
  chat: boolean;
  knowledge: boolean;
}): TourSteps {
  const { section, item, chat, knowledge } = options;
  switch (section) {
    case 'commits':
      return item
        ? [
            {
              target: 'nx-repository-change .actions',
              title: '交給 AI 初檢',
              description:
                '「AI 變更摘要」說明改了什麼，「AI 檢閱」找有證據的重大缺陷；在背景執行，完成時會通知你。',
              side: 'bottom',
            },
          ]
        : [
            {
              target: 'nx-repository-commits .commit',
              title: '選兩個提交看區間',
              description:
                '點左側圓點選兩個提交，就能看區間內的所有變更（含較舊那一個）並交給 AI；點標題看單一提交。',
            },
          ];
    case 'pulls':
    case 'issues': {
      const pulls = section === 'pulls';
      if (item)
        return pulls
          ? [
              {
                target: 'nx-repository-thread .actions',
                title: '檢閱這個 Pull Request',
                description: `「AI 檢閱」或「AI 變更摘要」比較合併基準到 head，完成後可預覽並貼回 PR 留言；「查看變更」看完整差異。${chat ? '「帶入對話」把說明與留言放進新對話的草稿，長度允許時附上差異。' : ''}`,
                side: 'bottom',
              },
            ]
          : [
              {
                target: 'nx-repository-thread .actions',
                title: chat ? '把議題帶入對話' : '到 Gitea 回覆',
                description: chat
                  ? '「帶入對話」把議題內文與留言打包成附件，放進新對話的草稿，確認後再送出。'
                  : '這裡只供閱讀；要留言或變更狀態，請在 Gitea 開啟。',
                side: 'bottom',
              },
            ];
      return [
        {
          target: 'nx-repository-threads .filters',
          title: '依狀態篩選',
          description: pulls
            ? '在開啟中、已關閉／合併與全部之間切換。'
            : '在開啟中、已關閉與全部之間切換，也能搜尋標題或內容。',
          side: 'bottom',
        },
        {
          target: 'nx-repository-threads .row',
          title: pulls ? '打開 Pull Request' : '打開議題',
          description: pulls
            ? '詳情含說明與留言，可查看變更或交給 AI 檢閱。'
            : '詳情含內文與最多 100 則留言。',
        },
      ];
    }
    case 'reviews':
      return item
        ? [
            {
              target: 'nx-repository-review .report',
              title: '整體報告',
              description:
                '繁體中文結論、變更重點與有證據的問題；這是靜態初檢，仍需核對上下文並執行測試。',
            },
          ]
        : [
            {
              target: 'nx-repository-reviews .intro',
              title: '檢閱目前版本',
              description: '把目前版本的最新提交交給 AI，在背景產生檢閱或摘要，完成時會通知你。',
              side: 'bottom',
            },
            {
              target: 'nx-repository-reviews .row',
              title: '你的檢閱',
              description: '這裡只列出你建立的檢閱與狀態；點選看報告與分段明細。',
            },
          ];
    default:
      return item
        ? [
            {
              target: 'nx-repository-code nx-repository-file',
              title: '閱讀檔案',
              description: `有行號與語法上色，Markdown 可切換預覽與原始碼。${chat ? '「帶入對話」把這個檔案放進新對話的草稿。' : ''}`,
            },
          ]
        : [
            {
              target: 'nx-repository-code .entries',
              title: '瀏覽資料夾與檔案',
              description: '點資料夾往下、點檔案看行號與語法上色；資料夾的 README 顯示在清單下方。',
            },
            chat && {
              target: 'nx-repository-code nx-checkbox.pick',
              title: '勾選檔案帶入對話',
              description:
                '可跨資料夾勾選最多 20 個檔案，打包成一個附件放進新對話的草稿，不會自動送出。',
              side: 'bottom',
            },
            knowledge && {
              target: 'nx-repository-code .repository-import',
              title: '匯入知識庫',
              description:
                '把目前資料夾的文字檔匯入你能編輯的知識庫，由背景任務逐檔處理；之後不會跟著 Gitea 更新。',
              side: 'bottom',
            },
          ];
  }
}

/**
 * 單一儲存庫的導覽。步驟依已開啟的區段、其中一個項目是否開啟，
 * 以及檢視者是否有聊天與知識庫功能而定；空的儲存庫沒有可顯示的 ref 或內容。連線選單永遠排在最後。
 */
export function repositoryTour(options: {
  section: RepositorySection;
  /** 區段中有檔案、提交或範圍、議題、拉取請求或審查被開啟。 */
  item: boolean;
  /** 儲存庫還沒有任何提交。 */
  empty: boolean;
  chat: boolean;
  knowledge: boolean;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-repository-detail .sections',
        title: '程式碼、紀錄與討論',
        description:
          '在程式碼、提交紀錄、Pull Request、議題與 AI 檢閱之間切換；標籤上的數字是開啟中的數量。',
        side: 'bottom',
      },
      !options.empty && {
        target: 'nx-repository-detail .ref-select',
        title: '固定閱讀的版本',
        description:
          '選擇分支或標籤後固定到一個提交（旁邊的短 SHA，點一下複製），之後閱讀的內容不會混入新的推送。',
        side: 'bottom',
      },
      ...(options.empty ? [] : sectionSteps(options)),
      {
        target: 'nx-repositories-page .pin-toggle',
        title: '釘選常用的程式庫',
        description: '釘選後顯示在程式庫列表上方（最多 30 個），再按一次取消。',
        side: 'bottom',
      },
      connection(),
    ],
  };
}
