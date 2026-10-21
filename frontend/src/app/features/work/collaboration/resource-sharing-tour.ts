import type { ProductTourDefinition } from '@shared/ui/product-tour';

/**
 * 存取權限對話框的導覽，知識庫、專案、成果文件與評測集共用；資源名詞由呼叫端依種類帶入。
 * 對話框只有擁有者打得開（後端讀寫 ACL 都只認擁有者），所以沒有非擁有者的版本。
 */
export function resourceSharingTour(options: {
  /** 資源種類的名詞，例如「專案」。 */
  noun: string;
  /** 對方找到這份資源的列表頁名稱（功能名稱，可被管理員改名）。 */
  listName: string;
  /** 唯讀快照分享的功能名稱；用來區分兩種「給別人看」。 */
  sharingName: string;
  /** 目前已有具名成員。 */
  members: boolean;
  /** 這種資源允許群組授權，且有可選的群組。 */
  groups: boolean;
  /** 正以測試身分檢視；儲存會實際改到該使用者的資源。 */
  testing: boolean;
}): ProductTourDefinition {
  const { noun } = options;
  return {
    steps: [
      {
        target: 'nx-resource-sharing nx-dialog-header',
        title: `誰能存取這個${noun}`,
        description: `只有擁有者能管理成員；這是持續有效的協作權限，對方看到的是最新內容。只想給一份固定的唯讀快照時，改用「${options.sharingName}」。`,
        side: 'bottom',
      },
      {
        target: 'nx-resource-sharing .sharing-search',
        title: '搜尋並加入成員',
        description:
          '輸入至少兩個字搜尋帳號或姓名，點結果即加入，預設為可檢視。只找得到登入過平台的帳號，你自己與已加入的人不會出現。',
        side: 'bottom',
      },
      {
        target: 'nx-resource-sharing .acl-members',
        title: '具名成員',
        description: options.members
          ? `這裡列出目前可以存取${noun}的人，最多 50 位；擁有者不在清單中，永遠保有完整權限。`
          : `目前只有你能存取這個${noun}；加入的成員會列在這裡，最多 50 位。`,
      },
      options.members && {
        target: 'nx-resource-sharing .acl-member nx-select',
        title: '可檢視與可編輯',
        description: `可檢視只能閱讀；可編輯還能修改${noun}的內容。兩者都不能管理存取權限或刪除${noun}，那只屬於擁有者。`,
        side: 'left',
      },
      options.members && {
        target: 'nx-resource-sharing .member-remove',
        title: '移除成員',
        description: `從清單移除，儲存後對方立即無法再開啟這個${noun}；群組授權不受影響。`,
        side: 'left',
      },
      options.groups && {
        target: 'nx-resource-sharing .sharing-groups',
        title: '群組只能檢視',
        description: '勾選的群組成員都能閱讀，但不會因此取得編輯能力；要一起編輯請加入具名成員。',
      },
      {
        target: 'nx-resource-sharing .sharing-save',
        title: '儲存後才生效',
        description: options.testing
          ? `測試身分期間儲存會實際改變該使用者這個${noun}的成員；取消則放棄這次所有變更。`
          : `變更在儲存前只留在畫面上，取消即放棄。儲存後成員會在「${options.listName}」列表看到它，卡片標示「共用 · 可閱讀」或「共用 · 可編輯」。`,
        side: 'top',
      },
    ],
  };
}
