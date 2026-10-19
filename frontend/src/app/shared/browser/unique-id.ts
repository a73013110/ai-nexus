let sequence = 0;

/**
 * 共用元件每個實例的 DOM ID（`aria-*`、`for`、`popovertarget` 的關聯）。整個應用程式共用一個計數，
 * 同一頁有多個實例時也不會撞號；ID 只求唯一，不帶意義，不要拿來當選擇器。
 */
export const uniqueId = (prefix: string) => `${prefix}-${++sequence}`;
