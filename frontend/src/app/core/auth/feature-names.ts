import type { FeatureId } from '../api/schema';

export type { FeatureId };

/**
 * `database/App/Seed/accesscontrol.sql` 的預設功能名稱，用於 `/me` 無法提供名稱的情況：載入前，
 * 以及此帳號未被授予的功能（存取提示、稽核）。管理員定義的、來自 `/me` 的名稱永遠優先。
 * 鍵是後端 `FeatureIds` 經 OpenAPI 產生的 `FeatureId`，後端增減功能而這裡沒跟上就無法編譯。
 */
export const FEATURE_NAMES: Readonly<Record<FeatureId, string>> = {
  dashboard: '總覽',
  chat: '對話',
  files: '檔案庫',
  projects: '專案',
  knowledge: '知識庫',
  artifacts: '成果文件',
  shared: '分享',
  quality: '品質評測',
  repositories: '程式庫',
  tasks: '背景任務',
  integrations: '資料來源',
  admin: '存取管理',
  operations: '平台營運',
  monitoring: '即時監控',
  audit: '活動稽核',
  'logs.query': '系統日誌',
  'logs.detail': '日誌診斷詳情',
  'logs.export': '日誌匯出',
  design: '介面元件',
};

/** 來自 API 的任意字串（稽核目錄、管理清單）是否為程式已知的功能 id。 */
export function isFeatureId(id: string): id is FeatureId {
  return Object.hasOwn(FEATURE_NAMES, id);
}
