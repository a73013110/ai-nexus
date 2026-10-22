# 資料範圍

授權是（群組, 功能, 範圍）：功能決定「能不能用這一頁」，範圍決定「看得到哪些人的資料」。範圍的種類與取捨見 [ADR 0017](../decisions/0017-organization-units-and-data-scopes.md)；授權本身見 [身分、角色、群組與功能](access-control.md)。

## 資料

- `accesscontrol.RoleGroupFeatures.Scope`：`own`、`assigned`、`unit`、`unit-tree`、`all` 其中一個；null 表示功能沒有資料列概念。資料庫只檢查代碼合法（`ck_RoleGroupFeatures_Scope`），「這個功能能用哪些範圍」由程式檢查。
- 功能在 [`FeatureCatalog`](../../backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs) 以 `Scopes` 宣告支援的範圍（由窄到寬）。**宣告了就表示查詢已套用範圍**；還沒套用的功能不要宣告，否則管理員設了範圍卻沒有效果。
- 群組儲存（`PUT /api/v1/admin/groups/{id}` 的 `scopes`）：有宣告範圍的功能必須給一個支援的值，沒宣告的不可帶，否則 `invalid_data_scope`（同樣留下稽核）。管理介面只列該功能支援的選項，新勾選的功能預設最窄的範圍。
- 目錄經 OpenAPI 的 `x-feature-catalog` 輸出每個功能的 `scopes`、`managerScope`（[主管範圍](#主管範圍)）與範圍名稱，前端的 `CATALOG_SCOPES` 由它產生。
- Seed 的預設授權也要符合宣告（`FeatureCatalogSeedTests`）。

## 讀取：`IDataScope`

[`IDataScope.ForAsync(userId, featureId)`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/IDataScope.cs) 回傳有效範圍：

- 各有效群組（角色、群組、功能都啟用）的範圍以 OR 合併；與功能授權同一次 SQL 讀取（`AccessService`），同一個 request 內不重讀授權。
- 目錄不支援的值（舊資料、目錄縮減）與 null 都不算，`IsEmpty`＝沒有資料：失敗即拒絕。
- `UnitIds`：`unit` 是使用者目前所屬的有效單位，`unit-tree` 再加上所有有效下層，再 OR 上[主管範圍](#主管範圍)；有 `all` 時不計算。停用的單位連同下層、停用或刪除的帳號都不算。所屬（含主管勾選）與下層各讀一次，不隨單位數增加。
- 對沒有宣告範圍的功能呼叫會拋出（程式錯誤）。

單位由 AccessControl 定義的 [`IUnitMembership`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/IUnitMembership.cs) 計算，Organization 的 `OrganizationDirectory` 實作；AccessControl 不依賴 Organization（`ModuleBoundaryTests`）。

## 主管範圍

範圍原則上只來自群組授權；例外是功能在目錄宣告 `ManagerScope`，讓組織管理勾選為主管（`organization.UnitMembers.IsManager`）的人自動取得範圍，不必另建主管群組。理由與取捨見 [ADR 0017](../decisions/0017-organization-units-and-data-scopes.md#主管範圍)。

- 值只能是 `unit` 或 `unit-tree`，且必須在該功能的 `Scopes` 內（`FeatureCatalogTests`）。
- 只算此人**任主管**的有效單位（`unit-tree` 再加有效下層），同時所屬但不是主管的單位不加入；取消勾選、單位停用、帳號停用都在下一個請求失效。
- 不授予功能本身：要先有該功能的授權（沒有仍是 403）；授權沒有可用的群組範圍時，只靠主管範圍。結果的 `Scopes` 也含這個代碼，模組照一般範圍處理，不需要分辨來源。
- 何時宣告：主管看部屬資料是業務上的預設（出勤）。主管不該自動看到的（稽核、對話）不宣告；宣告後管理員只能改主管勾選或從目錄拿掉宣告，無法逐群組關閉。

## 在模組裡套用

1. 在目錄宣告該功能支援的範圍，Seed 的預設授權給一個值（通常 administrators 給 `all`），開發庫加變更腳本回填既有授權。
2. handler 取 `IDataScope`，自己把範圍轉成查詢條件（EF 的 `Where` 或預存程序參數），寫在查詢裡；不用 EF 全域查詢篩選（不知道是哪個功能在查，也能被 `IgnoreQueryFilters` 繞過）。
3. 業務資料寫入時有存單位 ID 的，直接比對 `UnitIds`；沒有存的，用 `OrganizationDirectory.MemberIds(unitIds)` 換成目前成員，它是可組合的子查詢，整段仍是一個 SQL。
4. `assigned` 的關係（專案成員、負責客戶）由業務模組自己判斷，平台只回答有沒有這個範圍。
5. 沒有範圍就沒有資料；單筆讀取查不到（含範圍外）一律 404，不透露資料存在。範圍不影響 policy：沒有功能授權仍是 403。
6. 整合測試至少涵蓋各宣告範圍、多群組 OR、沒有範圍與停用單位；宣告了主管範圍的再加一個主管情境。

## 目前套用的功能

| 功能 | 範圍 | 主管範圍 | 條件 |
|---|---|---|---|
| `audit`（[活動稽核](../features/activity-audit.md#權限)） | `own`、`unit`、`unit-tree`、`all` | 無 | 紀錄的操作者或對象是範圍內的人（`AuditDataScope`） |
| `attendance.records`（[出勤紀錄](../features/attendance-records.md#資料範圍)） | `own`、`unit`、`unit-tree`、`all` | `unit-tree` | 持卡人是範圍內的人（`ListAttendanceRecords`） |
