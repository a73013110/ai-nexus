import type { ProductTourDefinition, TourSteps } from '@shared/ui/product-tour';

/** 組織頁的導覽；測試身分期間所有異動都被停用，略過寫入步驟並改說明唯讀。 */
export function organizationTour(options: {
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
  /** 有 `organization.import`，頁首有「從公文系統匯入」。 */
  canImport?: boolean;
}): ProductTourDefinition {
  const steps: TourSteps = [
    {
      target: 'nx-organization-page .feature-title',
      title: '部門決定資料範圍',
      description:
        '部門樹與人員所屬用來決定主管關係與「本部門、含下層」等資料範圍；能用哪些功能仍由角色決定，也不從 AD 同步。',
      side: 'bottom',
    },
    options.canImport && {
      target: 'nx-organization-page .organization-import',
      title: '從公文系統匯入',
      description: options.testing
        ? '可預覽公文系統的部門與主管會帶來哪些變動；測試身分期間不能套用。'
        : '部門與主管以公文系統為準時用：開啟只預覽新增、綁定同名手建單位、更新與停用，確認後才套用；手建的單位不受影響。',
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-organization-page .organization-create',
      title: '新增單位',
      description: '從最上層（例如公司）開始，逐層加入處、部、課；組織最多八層。',
      side: 'bottom',
    },
    {
      target: 'nx-organization-page .organization-tree .unit-name',
      title: '瀏覽部門樹',
      description: '點單位查看它的直屬成員，箭頭可展開或收合下層；停用的單位與它的下層以灰字標示。',
      side: 'right',
    },
    {
      target: 'nx-organization-page .organization-tree .unit-source',
      title: '標「公文」的單位',
      description:
        '由公文系統匯入建立或綁定：名稱、上層、啟用、順序與主管以公文系統為準，在這裡修改只維持到下次匯入；成員與主要所屬仍在這裡維護。',
      side: 'right',
    },
    !options.testing && {
      target: 'nx-organization-page .organization-tree .unit-menu',
      title: '單位操作',
      description:
        '可編輯、新增下層、在同層上移或下移；刪除只限沒有下層與成員的單位，不再使用的建議改為停用。',
      side: 'right',
    },
    !options.testing && {
      target: 'nx-organization-page .organization-actions',
      title: '編輯、搬移與停用',
      description:
        '編輯可改名、換上層（整棵下層跟著搬）或停用；停用的單位保留成員，但與所有下層都不參與資料範圍。',
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-organization-page .organization-add-members',
      title: '加入成員',
      description:
        '開啟全部在職使用者的名單，依主要所屬分組、可篩選，勾選多人一次加入；尚未分派部門的人排在最前面。停用的單位不能加入。',
      side: 'bottom',
    },
    {
      target: 'nx-organization-page .organization-members .person-list',
      title: '主要所屬與主管',
      description: options.testing
        ? '列出直屬成員、主要所屬與主管；測試身分期間只能查看。'
        : '一人可屬多個單位但只有一個主要所屬；主管只看直屬單位，不延伸到下層。',
      side: 'top',
    },
  ];
  return { steps };
}

/** 單位編輯對話框的導覽；新增時上層只是放置位置，編輯時換上層即為搬移。 */
export function unitDialogTour(options: {
  /** 編輯既有單位（而非新增）。 */
  editing: boolean;
  /** 正以測試身分檢視，欄位唯讀。 */
  testing: boolean;
  /** 編輯的是來自公文系統的單位（`gdDepNo` 有值）。 */
  imported?: boolean;
}): ProductTourDefinition {
  return {
    steps: [
      options.imported && {
        target: 'nx-unit-dialog .unit-imported',
        title: '下次匯入會改回',
        description:
          '這個單位由公文系統管理：這裡的修改只維持到下次匯入。要保留（例如停用），匯入時不勾選這個部門；長期請改公文系統。',
        side: 'bottom',
      },
      {
        target: 'nx-unit-dialog .unit-name-field',
        title: '單位名稱',
        description: options.testing
          ? '同一個上層底下名稱不可重複；測試身分期間只能查看。'
          : '同一個上層底下名稱不可重複。',
        side: 'bottom',
      },
      {
        target: 'nx-unit-dialog .unit-parent-field',
        title: options.editing ? '搬到其他上層' : '放在哪個上層',
        description: options.editing
          ? '換上層時整棵下層跟著搬、成員不變；選單不列出自己、下層與搬過去會超過八層的單位。'
          : '新單位排到所選上層的最後，可之後在列表用上移、下移調整順序。',
        side: 'bottom',
      },
      {
        target: 'nx-unit-dialog .unit-enabled',
        title: '啟用與停用',
        description: '停用的單位保留成員與歷史，但它與所有下層都不參與資料範圍，也不能再加入成員。',
        side: 'top',
      },
    ],
  };
}

const IMPORT = 'nx-unit-import-dialog';

/**
 * 從公文系統匯入對話框的導覽：預覽的各區只在有內容時出現（沒有的區由導覽略過）。
 * 測試身分可以預覽、不能套用。
 */
export function unitImportTour(options: {
  /** 正以測試身分檢視，不能套用。 */
  testing: boolean;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${IMPORT} .import-summary`,
        title: '先看變動摘要',
        description:
          '開啟時只預覽、不寫入；摘要列出會新增、綁定手建單位、更新與停用的單位，以及設定或取消的主管。',
        side: 'bottom',
      },
      {
        target: `${IMPORT} .import-rule`,
        title: '以公文系統為準',
        description:
          '匯入的單位名稱、上層、啟用、順序與主管每次都覆蓋；公文系統已沒有的部門只停用、不刪除，成員與歷史保留。',
        side: 'bottom',
      },
      {
        target: `${IMPORT} .import-units`,
        title: '勾選要套用的部門',
        description:
          '依新增、綁定（同一層的同名手建單位改由公文系統管理）、更新、停用分組，每個部門註明改了什麼與主管的變動。未勾選的部門這次不動、下次仍會列出；本地已停用、套用會重新啟用的部門預設不勾。',
      },
      {
        target: `${IMPORT} .import-issues`,
        title: '略過的問題',
        description:
          '這些部門或主管帳號不會匯入，其他變動照常套用；請在公文系統修正，或讓對應的人先登入平台後再匯入。',
      },
      {
        target: `${IMPORT} .unit-import-apply`,
        title: '套用匯入',
        description: options.testing
          ? '測試身分期間只能預覽，不能套用。'
          : '套用時會重新比對；預覽之後公文系統或組織有人改過就拒絕並請你重新預覽，不會套用沒看過的變更。',
        side: 'top',
      },
    ],
  };
}
