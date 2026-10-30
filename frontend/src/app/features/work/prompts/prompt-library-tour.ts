import type { ProductTourDefinition } from '@shared/ui/product-tour';

/**
 * 常用提示詞對話框的導覽；編輯器說明依目前是新增還是編輯既有範本切換。
 * 範本只屬於本人，沒有共用範本或管理權限的差異。
 */
export function promptLibraryTour(options: {
  /** 編輯器正在編輯既有範本（而非新增）。 */
  editing: boolean;
  /** 正以測試身分檢視，看到與保存的是該使用者的範本。 */
  testing: boolean;
}): ProductTourDefinition {
  return {
    steps: [
      {
        target: 'nx-prompt-library .template-list',
        title: '你的常用範本',
        description: options.testing
          ? '測試身分期間列出的是該使用者自己的範本，新增或修改會實際保存到對方名下。'
          : '把常用的提問存成範本，只有你自己看得到。\n最近更新的排在前面，每人最多 100 個。\n也可按 Ctrl + Shift + L 開啟。',
        side: 'right',
      },
      {
        target: 'nx-prompt-library .template-use',
        title: '套用到輸入框',
        description: '點範本會關閉對話框，並以範本內容取代輸入框目前的草稿；送出前仍可修改。',
        side: 'right',
      },
      {
        target: 'nx-prompt-library .template-create',
        title: '新增範本',
        description: '新增時會先帶入輸入框目前的草稿，適合把剛寫好的提問直接存起來。',
        side: 'bottom',
      },
      {
        target: 'nx-prompt-library .template-edit',
        title: '編輯範本',
        description: '把範本載入右側編輯器；儲存會更新原範本，不會另存一份。',
      },
      {
        target: 'nx-prompt-library .template-delete',
        title: '刪除範本',
        description: '按下立即刪除、不再確認，也無法復原；正在編輯的範本被刪除時編輯器回到新增。',
      },
      {
        target: 'nx-prompt-library .template-editor',
        title: options.editing ? '正在編輯' : '撰寫範本',
        description: options.editing
          ? '儲存後覆寫列表中標示的範本；按「新增範本」可放棄這次編輯、改為新增。'
          : '名稱最多 80 字、內容最多 12000 字，兩者都必填；儲存後出現在左側列表。',
        side: 'left',
      },
    ],
  };
}
