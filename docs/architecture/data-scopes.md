# 資料範圍

授權是（群組, 功能, 範圍）：功能決定「能不能用這一頁」，範圍決定「看得到哪些人的資料」。範圍的種類與取捨見 [ADR 0017](../decisions/0017-organization-units-and-data-scopes.md)；授權本身見 [身分、角色、群組與功能](access-control.md)。

## 資料

- `accesscontrol.RoleGroupFeatures.Scope`：`own`、`assigned`、`unit`、`unit-tree`、`all` 其中一個；null 表示功能沒有資料列概念。資料庫只檢查代碼合法（`ck_RoleGroupFeatures_Scope`），「這個功能能用哪些範圍」由程式檢查。
- 功能在 [`FeatureCatalog`](../../backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs) 以 `Scopes` 宣告支援的範圍（由窄到寬）。**宣告了就表示查詢已套用範圍**；還沒套用的功能不要宣告，否則管理員設了範圍卻沒有效果。
- 群組儲存（`PUT /api/v1/admin/groups/{id}` 的 `scopes`）：有宣告範圍的功能必須給一個支援的值，沒宣告的不可帶，否則 `invalid_data_scope`（同樣留下稽核）。管理介面只列該功能支援的選項，新勾選的功能預設最窄的範圍。
- 目錄經 OpenAPI 的 `x-feature-catalog` 輸出每個功能的 `scopes` 與範圍名稱，前端的 `CATALOG_SCOPES` 由它產生。
- Seed 的預設授權也要符合宣告（`FeatureCatalogSeedTests`）。

## 讀取：`IDataScope`

[`IDataScope.ForAsync(userId, featureId)`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/IDataScope.cs) 回傳有效範圍：

- 各有效群組（角色、群組、功能都啟用）的範圍以 OR 合併；與功能授權同一次 SQL 讀取（`AccessService`），同一個 request 內不重讀授權。
- 目錄不支援的值（舊資料、目錄縮減）與 null 都不算，`IsEmpty`＝沒有資料：失敗即拒絕。
- `UnitIds`：`unit` 是使用者目前所屬的有效單位，`unit-tree` 再加上所有有效下層；有 `all` 時不計算。停用的單位連同下層、停用或刪除的帳號都不算。
- 對沒有宣告範圍的功能呼叫會拋出（程式錯誤）。

單位由 AccessControl 定義的 [`IUnitMembership`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/IUnitMembership.cs) 計算，Organization 的 `OrganizationDirectory` 實作；AccessControl 不依賴 Organization（`ModuleBoundaryTests`）。

## 在模組裡套用

1. 在目錄宣告該功能支援的範圍，Seed 的預設授權給一個值（通常 administrators 給 `all`），開發庫加變更腳本回填既有授權。
2. handler 取 `IDataScope`，自己把範圍轉成查詢條件（EF 的 `Where` 或預存程序參數），寫在查詢裡；不用 EF 全域查詢篩選（不知道是哪個功能在查，也能被 `IgnoreQueryFilters` 繞過）。
3. 業務資料寫入時有存單位 ID 的，直接比對 `UnitIds`；沒有存的，用 `OrganizationDirectory.MemberIds(unitIds)` 換成目前成員，它是可組合的子查詢，整段仍是一個 SQL。
4. `assigned` 的關係（專案成員、負責客戶）由業務模組自己判斷，平台只回答有沒有這個範圍。
5. 沒有範圍就沒有資料；單筆讀取查不到（含範圍外）一律 404，不透露資料存在。範圍不影響 policy：沒有功能授權仍是 403。
6. 整合測試至少涵蓋各宣告範圍、多群組 OR、沒有範圍與停用單位。

## 目前套用的功能

| 功能 | 範圍 | 條件 |
|---|---|---|
| `audit`（[活動稽核](../features/activity-audit.md#權限)） | `own`、`unit`、`unit-tree`、`all` | 紀錄的操作者或對象是範圍內的人（`AuditDataScope`） |
