# 存取管理

存取管理是五個頁面，各自一個授權（窄螢幕改為卡片列，點卡片開詳情或編輯）：使用者 `/admin/identity/users`（`identity.users`）、角色 `/admin/access-control/roles`（`access-control.roles`）、功能群組與模型 `/admin/access-control/groups`（`access-control.groups`）、功能 `/admin/access-control/features`（`access-control.features`）、選單 `/admin/access-control/navigation`（`access-control.navigation`）。一般登入不會取得管理權。API 每次查詢與異動都重新驗證，寫入另要求 CSRF。

部門樹與人員所屬在同一個「身分與權限」分群的 [組織](organization.md) 頁，另有授權。平台用量、檢索管理與價格見 [平台營運](operations.md)；稽核、監控、日誌各自從側欄進入：[活動稽核](activity-audit.md)、[系統日誌](system-logs.md)、[即時監控](monitoring.md)。相關頁面：[模型政策與配額](model-policy.md)、[測試身分](test-identity.md)。授權模型見 [身分與授權](../architecture/access-control.md)。

## 設定第一位管理員

在本機 `.local/config/appsettings.Local.json` 或正式主機的一般設定加入：

```json
{
  "Identity": {
    "Bootstrap": { "Administrators": ["it.admin"] }
  }
}
```

填 AD 短帳號（不含密碼、DN 或網域前綴）後重啟。該帳號下一次通過 AD 驗證時取得 `administrator` 角色，並在 `identity.AdministratorBootstraps` 留下唯一標記與稽核。移除角色後不會因再次登入而補回；正式上線後可清空清單，改由管理後台授權。

## 使用者

- 點使用者列開啟使用者詳情，是管理單一使用者的地方：頂部是身分、狀態、登入方式、角色與 30 天用量，頁籤為「帳號」（姓名、啟用、登入方式與密碼）、「通知信箱」、「角色與權限」（含儲存後可用功能預覽）、「AI 模型」「附件容量」「對話」。可編輯的頁籤各自儲存，切換或關閉時未儲存的變更會先確認；測試身分期間全部唯讀。
- 「新增使用者」只填帳號與角色；模型政策、容量與活動在建立後從詳情調整。列的「⋯」選單只放「以此身分測試」與「刪除使用者」（需確認）。
- 每位使用者可啟用 AD、本地密碼或兩者，兩種登入對應同一個 UserId、角色與私人資料。
- 預建的 AD 帳號填目錄短帳號，首次成功驗證才綁定 SID；已綁定的帳號不能換綁他人。手動輸入的姓名不會被目錄登入覆蓋。
- 通知信箱：列表顯示信箱與來源、可搜尋；AD 提供的唯讀，其他可由管理員設定或清除（[規則](account-security.md#管理員設定信箱)）。
- 移除是邏輯刪除，保留歷史對話、稽核與登入名稱，防止重建同名帳號接管資料。
- 本地密碼採 Argon2id PHC 格式（64 MiB、3 iterations、parallelism 1、128-bit salt），API、前端與稽核只顯示「是否已設定」。密碼 12–128 字元；登入有 IP 限流，連續 5 次失敗暫鎖 15 分鐘，管理員重設密碼可解除。本地姓名與 bootstrap 帳號相同也不會取得管理權。
- 停用、修改登入政策或重設密碼會撤銷舊 cookie。管理員不能移除自己、停用自己目前的登入方式或撤銷自己的管理權。

## 授權工作流程

1. 使用者首次登入建立平台身分與 `member` 角色（`workspace` 群組）。
2. 「功能群組與模型」新增群組，勾選功能，視需要限制模型與配額。有資料範圍的功能（例如活動稽核）勾選後要選範圍：本人、所屬單位、所屬單位及下層或全部，選項只列該功能支援的，預設最窄；多個群組取聯集，所屬單位在[組織](organization.md)頁維護（[資料範圍](../architecture/data-scopes.md)）。
3. 「角色」建立工作角色並加入群組。
4. 「使用者」搜尋帳號，在詳情的「角色與權限」勾選角色；儲存前可預覽可用功能，儲存後下一次 API 操作生效。

- 主檔用穩定 ID，名稱可改。角色與群組只能停用、不刪除，保留授權歷史；功能列由 `database/App/Seed/` 寫入，管理員在「功能」頁只調整啟用狀態與名稱覆寫（留空＝程式目錄的預設名稱）；圖示與網址來自程式目錄。
- 異動在同一個 serializable 交易保存授權與稽核。會撤銷操作者自身管理權的變更回 409 並回滾，交由另一位管理員執行。異動 JSON 只含授權、模型識別與限制。
- 授權清單（儲存後可用功能、群組列表與群組編輯的功能、稽核的功能異動）和側欄一樣依選單分組：標題是分群路徑（如「系統管理 › 身分與權限」），組內依選單順序；功能放在它在選單第一次出現的位置，動作跟著上層頁面，沒放進選單的歸在「未放入選單」，程式目錄不認得的 ID 歸在最後的「其他」。分組用完整的選單樹，不受檢視者自己的授權影響（[規則](../architecture/access-control.md#選單)）。存取預覽與 `/me` 以同一套規則算出使用者看得到的選單。

## 選單

「選單」頁編輯整棵側欄樹，包含空分群與使用者看不到的節點。選單只決定入口的排列，不授予功能；使用者看到的是伺服器依授權修剪過的樹（規則與資料模型見 [身分與授權](../architecture/access-control.md#選單)）。

- **分群**：名稱與圖示，可巢狀，最多 4 層。仍有子節點時不能刪除（`navigation_item_not_empty`）；不能移到自己底下（`navigation_item_cycle`）或超過層數（`navigation_depth_exceeded`）。
- **頁面**：從程式目錄的頁面挑選；同一頁面可放在多處，但同一層只能一次（`navigation_page_duplicate`）。名稱跟著功能走，改名在「功能」頁。沒有授權或已停用的頁面不會出現在使用者的選單。
- **外部連結**：名稱、`https://` 網址、圖示、是否另開分頁與可見群組（留空＝所有登入者）。主機要在維運設定的允許清單內（`navigation_link_host_not_allowed`，見 [IIS 外部設定](../operations/iis-configuration.md#外部連結允許清單)）；清單預設是空的。可見群組只決定入口是否出現，外部系統仍自行驗證權限。
- **排序與移動**：拖曳或鍵盤調整順序、跨層移動；整批位置一次送出，全部檢查通過才寫入，失敗時畫面回到原狀。成功後重新讀取 `/me`，側欄跟著更新。
- 沒有可見項目的分群不顯示，只剩一項的分群攤平成該項；空分群因此可以先建好再放內容。
- 稽核動作為 `admin.navigation_item`、`admin.navigation_item_delete`、`admin.navigation_order`，在活動稽核以「選單異動」篩選。

## 使用者用量與對話

- 使用者列表與詳情頂部顯示最近 30 天的生成次數、回報 tokens、對話數與原檔容量。
- 用量由共用的 `UsageReports` 統計 `GenerationRuns` 與 `ModelInvocations`，涵蓋聊天、OCR、文字工具、評測與耗時；期間是最近 30 個 UTC 日期，顯示用台北時區，未回報的 token 不估算。
- 每次回答可展開總耗時、排隊、執行與輸入／輸出 tokens；執行時間包含 Context 準備與等待 provider 容量，不是供應商的純運算時間。
- 詳情的「對話」頁籤第一次開啟時才列出對話，可搜尋標題與本文，包含封存與已軟刪除的對話；對話每頁 50 段、內容每次 100 則，顯示資料庫保存的所有分支。附件只顯示名稱與大小，不增加下載權限。
- 唯讀 API：`GET /api/v1/admin/users/{id}/insights`、`.../users/{id}/conversations`、`/api/v1/admin/conversations/{id}`。每次重新檢查 `identity.users`，回應前先保存 `admin.user_usage_read`、`admin.conversations_list` 或 `admin.conversation_read` 稽核。管理員不能透過一般 API 修改他人的對話；登入頁會說明這項授權檢視。
