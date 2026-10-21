import type { TourStep } from './product-tour';

/**
 * 資料頁（系統日誌、活動稽核、寄送紀錄）共用元件的導覽步驟，讓同一個查詢列與游標分頁在各頁說法一致。
 * `host` 是頁面宿主標籤；頁面專屬的條件由呼叫端以名詞片語傳入。
 */
export function queryBarStep(
  host: string,
  options: {
    /** 列上的條件，例如「時間範圍、等級與搜尋」。 */
    fields: string;
    /** 收在「＋條件」的條件；沒有傳入就不提。 */
    more?: string;
  },
): TourStep {
  return {
    target: `${host} nx-query-bar`,
    title: '查詢條件',
    description: `可依${options.fields}篩選${
      options.more ? `，${options.more}收在「＋條件」` : ''
    }。改欄位只是草稿，按「查詢」或 Enter 才套用；已套用的條件成為可逐一移除的標籤並寫進網址，重新整理或分享連結會看到相同結果。`,
    side: 'bottom',
  };
}

/**
 * `nx-table-pagination` 的 `cursor` 模式：不計總數，換頁沿用已套用的條件。
 * 預設頁面有排序切換；固定由新到舊的頁面傳 `sortable: false`，文案改說明新紀錄不會讓翻頁重複。
 */
export function cursorPaginationStep(host: string, options: { sortable?: boolean } = {}): TourStep {
  return {
    target: `${host} nx-table-pagination`,
    title: '翻頁',
    description:
      options.sortable === false
        ? '紀錄固定由新到舊、不計總數，只有上一頁、下一頁與回到第一頁，新紀錄不會讓翻頁重複；要找較早的紀錄請縮小時間範圍。換頁與每頁筆數沿用已套用的條件，不寫進網址。'
        : '大表不計總數，只有上一頁、下一頁與回到第一頁，改用時間範圍與排序定位；換頁、排序與每頁筆數沿用已套用的條件，不寫進網址。',
  };
}
