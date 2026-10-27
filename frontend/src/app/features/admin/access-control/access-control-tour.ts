import type { ProductTourDefinition, TourStep, TourSteps } from '@shared/ui/product-tour';
import type { NavigationKind } from '@core/layout/navigation-tree';
import type { AccessKind } from './roles/access-editor-dialog';

/** 導覽文案引用的其他頁名稱；取 `session.featureName`，管理員改名後文案跟著變。 */
export interface AccessPageNames {
  users: string;
  roles: string;
  groups: string;
  features: string;
  navigation: string;
  organization: string;
}

/** 依功能 ID 取得各頁目前的顯示名稱。 */
export function accessPageNames(name: (id: string) => string): AccessPageNames {
  return {
    users: name('identity.users'),
    roles: name('access-control.roles'),
    groups: name('access-control.groups'),
    features: name('access-control.features'),
    navigation: name('access-control.navigation'),
    organization: name('organization'),
  };
}

const READ_ONLY = '測試身分期間只能查看。';

/** 測試身分期間對話框的儲存被停用；說明放在儲存列，而不是讓人以為按鈕壞了。 */
function readOnlyStep(host: string): TourStep {
  return {
    target: `${host} nx-dialog-save-bar, ${host} .dialog-foot`,
    title: '測試身分唯讀',
    description: '以其他使用者的身分檢視時，欄位唯讀、不能建立或儲存；結束測試身分後再修改。',
    side: 'top',
  };
}

/** 角色頁的導覽；角色在使用者頁指派，沒有該頁授權時改說明由誰指派。 */
export function rolesTour(options: {
  names: AccessPageNames;
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
  /** 檢視者能開使用者頁，自己就能把角色指派出去。 */
  assign: boolean;
}): ProductTourDefinition {
  const { names } = options;
  const steps: TourSteps = [
    {
      target: 'nx-roles-page .feature-title',
      title: '角色是授權的入口',
      description: `使用者取得角色，角色由功能群組組成；可用功能是所有角色的群組聯集。群組在「${names.groups}」頁維護。`,
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-roles-page nx-admin-roles-tab .page-toolbar .primary-button',
      title: '新增角色',
      description: '填識別碼與名稱、勾選功能群組；識別碼建立後不能修改。',
      side: 'bottom',
    },
    {
      target: 'nx-roles-page .admin-roles-table td[data-label="功能群組"]',
      title: '組成的功能群組',
      description: `角色本身不授予功能，內容全來自這些群組；群組勾了哪些功能、模型與容量，到「${names.groups}」頁查看。`,
    },
    {
      target: 'nx-roles-page .admin-roles-table td[data-label="使用者"]',
      title: '指派給使用者',
      description: options.assign
        ? `目前擁有此角色的人數；在「${names.users}」頁的使用者詳情勾選角色，下一次操作生效。`
        : `目前擁有此角色的人數；角色由有「${names.users}」頁授權的管理員在使用者詳情指派。`,
    },
    {
      target: 'nx-roles-page .admin-roles-table .ui-table-link',
      title: '編輯角色',
      description: options.testing
        ? `點名稱查看組成與儲存後可用的功能；${READ_ONLY}`
        : '點名稱調整名稱、啟用與群組，並試算儲存後可用的功能；角色只能停用、不能刪除，以保留授權歷史。',
    },
  ];
  return { steps };
}

/** 功能群組頁的導覽。 */
export function groupsTour(options: {
  names: AccessPageNames;
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
}): ProductTourDefinition {
  const { names } = options;
  const steps: TourSteps = [
    {
      target: 'nx-groups-page .feature-title',
      title: '功能群組是授權的單位',
      description: `群組授予功能、AI 模型與附件容量，但不直接給人；要加入「${names.roles}」頁的角色，擁有該角色的使用者才取得。`,
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-groups-page nx-admin-groups-tab .page-toolbar .primary-button',
      title: '新增功能群組',
      description: '建立後勾選功能並視需要限制模型；之後新增的功能不會自動加進既有群組。',
      side: 'bottom',
    },
    {
      target: 'nx-groups-page .admin-groups-table td[data-label="AI 模型"]',
      title: '模型與附件容量',
      description:
        '使用者屬於多個群組時，模型取聯集、每日額度取最高值（無上限優先）、附件容量取最低值；個人政策另行套用。',
    },
    {
      target: 'nx-groups-page .admin-groups-table td[data-label="功能授權"]',
      title: '授予的功能',
      description: `依側欄的位置分組列出；功能的名稱與開關在「${names.features}」頁，停用的功能即使勾選也不授予。`,
    },
    {
      target: 'nx-groups-page .admin-groups-table .ui-table-link',
      title: '編輯群組',
      description: options.testing
        ? `點名稱查看功能、資料範圍與模型政策；${READ_ONLY}`
        : '點名稱調整功能、資料範圍、模型與容量；群組只能停用、不能刪除，以保留授權歷史。',
    },
  ];
  return { steps };
}

/** 功能頁的導覽。 */
export function featuresTour(options: {
  names: AccessPageNames;
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
}): ProductTourDefinition {
  const { names } = options;
  const steps: TourSteps = [
    {
      target: 'nx-features-page .feature-title',
      title: '功能是最小的授權',
      description: `每個頁面或動作是一項功能，由程式目錄定義；被「${names.groups}」頁的群組勾選，才會授予使用者。`,
      side: 'bottom',
    },
    {
      target: 'nx-features-page .admin-features-table .feature-route',
      title: '頁面與動作',
      description:
        '有路由的是頁面；標示「無頁面，僅權限」的是動作，只控制頁內的操作。網址與圖示不能修改。',
    },
    {
      target: 'nx-features-page .admin-features-table nx-access-status',
      title: '停用即全面收回',
      description:
        '停用的功能不授予任何人，也不出現在任何人的選單；群組的勾選保留，重新啟用即恢復。',
    },
    {
      target: 'nx-features-page .admin-features-table .ui-table-link',
      title: '改名或停用',
      description: options.testing
        ? `點名稱查看預設名稱與啟用狀態；${READ_ONLY}`
        : `點名稱改名（留白沿用預設名稱）或停用；新名稱同時用於側欄與頁面標題，側欄位置在「${names.navigation}」頁安排。`,
    },
  ];
  return { steps };
}

/** 選單頁的導覽；測試身分期間新增、移動與列操作都被停用。 */
export function navigationTour(options: {
  names: AccessPageNames;
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
}): ProductTourDefinition {
  const { names } = options;
  const steps: TourSteps = [
    {
      target: 'nx-navigation-page .feature-title',
      title: '選單是資料',
      description: `側欄的分群、頁面與外部連結存在資料庫，只決定入口排列、不授予功能；權限仍由「${names.roles}」與「${names.groups}」頁決定。`,
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-navigation-page nx-navigation-editor .page-toolbar nx-action-menu',
      title: '新增項目',
      description: '分群整理側欄，最多四層；頁面從功能目錄挑選；外部連結的網站須在系統允許清單內。',
      side: 'bottom',
    },
    !options.testing && {
      target: 'nx-navigation-page .navigation-table .navigation-handle',
      title: '調整順序與層級',
      description:
        '拖曳把手，或在把手上用上下鍵排序、左右鍵移出或移入分群；整批檢查通過才寫入，失敗時畫面回到原狀。',
      side: 'right',
    },
    {
      target: 'nx-navigation-page .navigation-table td[data-label="內容"]',
      title: '每個人看到的不同',
      description:
        '使用者只看到有權限且啟用的頁面與可見的連結；沒有可見項目的分群不顯示，只要有一項就照常顯示分群。',
    },
    !options.testing && {
      target: 'nx-navigation-page .navigation-table .navigation-row-menu',
      title: '更多操作',
      description: '可編輯、移到其他分群、在分群內新增或刪除；分群要先清空才能刪除。',
      side: 'left',
    },
    {
      target: 'nx-navigation-page .navigation-table .ui-table-link',
      title: '編輯項目',
      description: options.testing
        ? `點名稱查看設定；${READ_ONLY}`
        : `點名稱編輯分群或連結；頁面的名稱跟著功能，要改名請到「${names.features}」頁。`,
    },
  ];
  return { steps };
}

/** 角色、功能群組與功能共用的編輯對話框導覽，依編輯對象與群組目前的頁籤切換內容。 */
export function accessEditorTour(options: {
  names: AccessPageNames;
  kind: AccessKind;
  /** 新增（而非編輯既有項目），識別碼可填。 */
  creating: boolean;
  /** 功能群組目前在「AI 模型」頁籤。 */
  models: boolean;
  /** 正以測試身分檢視，欄位唯讀。 */
  testing: boolean;
}): ProductTourDefinition {
  const { names, kind } = options;
  const host = 'nx-access-editor-dialog';
  const basics: TourStep = {
    target: `${host} .access-basics`,
    title: options.creating ? '識別碼與名稱' : '名稱與啟用',
    description:
      (options.creating ? '識別碼是授權與稽核用的穩定 ID，建立後不能修改；' : '名稱可隨時修改；') +
      (kind === 'role'
        ? '停用後，擁有此角色的使用者不再取得它的群組。'
        : '停用後不再授予功能、模型或附件容量。'),
    side: 'bottom',
  };
  const tabs: TourStep = {
    target: `${host} nx-tabs`,
    title: '兩個頁籤一起儲存',
    description:
      '群組設定與 AI 模型分頁呈現，一次儲存涵蓋兩者；頁籤上的標記表示該頁有未儲存的變更。',
    side: 'bottom',
  };
  let steps: TourSteps;
  if (kind === 'feature')
    steps = [
      {
        target: `${host} .access-name`,
        title: '名稱覆寫',
        description: '留白或與預設名稱相同就沿用程式目錄的名稱，之後目錄改名也跟著變。',
        side: 'bottom',
      },
      {
        target: `${host} .access-basics nx-checkbox`,
        title: '啟用功能',
        description: `停用後任何群組都不授予此功能；授予給誰在「${names.groups}」頁，側欄位置在「${names.navigation}」頁。`,
        side: 'top',
      },
    ];
  else if (kind === 'role')
    steps = [
      basics,
      {
        target: `${host} .access-groups`,
        title: '加入功能群組',
        description: `可複選，可用功能取各群組的聯集；群組的內容在「${names.groups}」頁維護。`,
        side: 'top',
      },
      {
        target: `${host} .access-preview`,
        title: '儲存後可用功能',
        description: '依目前勾選試算，只計已啟用群組中已啟用的功能，並依側欄位置分組。',
        side: 'top',
      },
    ];
  else if (options.models)
    steps = [
      tabs,
      {
        target: `${host} nx-model-policy-editor`,
        title: '模型與每日額度',
        description:
          '限制這個群組可用的模型與每日 token 額度；多個群組時模型取聯集、額度取最高值，個人政策另行套用。',
        side: 'top',
      },
    ];
  else
    steps = [
      tabs,
      basics,
      {
        target: `${host} .access-features`,
        title: '功能授權',
        description: `依側欄位置分組勾選；停用的功能不授予，功能本身在「${names.features}」頁管理。`,
        side: 'top',
      },
      {
        target: `${host} .access-scopes`,
        title: '資料範圍',
        description: `有資料範圍的功能要選所有成員看得到誰的資料，部分功能可再讓主管多看所管單位；預設最窄、多群組取聯集，單位與主管在「${names.organization}」頁維護。`,
        side: 'top',
      },
      {
        target: `${host} .access-storage`,
        title: '附件容量',
        description: '多個群組取最低值，個人設定優先；降低上限只擋新的上傳。',
        side: 'top',
      },
    ];
  return { steps: [...steps, options.testing && readOnlyStep(host)] };
}

/** 選單項目對話框的導覽；種類只在新增時可選，各種類的欄位不同。 */
export function navigationItemTour(options: {
  names: AccessPageNames;
  kind: NavigationKind;
  /** 新增（而非編輯既有項目），可選種類與位置。 */
  creating: boolean;
  /** 正以測試身分檢視，欄位唯讀。 */
  testing: boolean;
}): ProductTourDefinition {
  const { names, kind } = options;
  const host = 'nx-navigation-item-dialog';
  const steps: TourSteps = [
    options.creating && {
      target: `${host} nx-view-switch`,
      title: '選擇種類',
      description: '分群、頁面或外部連結；建立後不能更改種類。',
      side: 'bottom',
    },
    options.creating && {
      target: `${host} .navigation-parent`,
      title: '放在哪裡',
      description: '選最上層或某個分群；建立後可在列表拖曳調整順序與層級。',
      side: 'bottom',
    },
    kind === 'page'
      ? {
          target: `${host} [data-field="featureId"]`,
          title: '挑選頁面',
          description: `名稱與圖示跟著功能，改名在「${names.features}」頁；同一層不能重複同一頁面，沒有權限的人看不到。`,
          side: 'bottom',
        }
      : {
          target: `${host} input[data-field="name"]`,
          title: kind === 'group' ? '分群名稱' : '連結名稱',
          description:
            kind === 'group'
              ? '分群只整理側欄、不影響授權；沒有可見項目的分群不會出現。'
              : '顯示在側欄的文字；圖示不設定時用外部連結圖示。',
          side: 'bottom',
        },
    kind === 'link' && {
      target: `${host} input[data-field="url"]`,
      title: '網址與允許清單',
      description: '只接受 https:// 的完整網址，網站須在維運設定的允許清單內，否則無法儲存。',
      side: 'bottom',
    },
    kind === 'link' && {
      target: `${host} .navigation-audience`,
      title: '可見群組',
      description:
        '不勾選＝所有登入者都看得到；勾選後只有屬於其中任一功能群組的人看得到。外部系統仍自行驗證權限。',
      side: 'top',
    },
    options.testing && readOnlyStep(host),
  ];
  return { steps };
}
