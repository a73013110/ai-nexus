# 資料範圍

授權是（群組, 功能, 範圍, 主管範圍）：功能決定「能不能用這一頁」，範圍決定群組所有成員「看得到哪些人的資料」，主管範圍是其中組織勾選為主管的人另可看的。範圍的種類與取捨見 [ADR 0017](../decisions/0017-organization-units-and-data-scopes.md)、主管範圍見 [ADR 0022](../decisions/0022-manager-scopes-on-grants.md)；授權本身見 [身分、角色、群組與功能](access-control.md)。

## 資料

- `accesscontrol.RoleGroupFeatures.Scope`：`own`、`assigned`、`unit`、`unit-tree`、`all` 其中一個；null 表示功能沒有資料列概念。`ManagerScope`：`managed`、`managed-tree` 或 null（不另外）。資料庫只檢查代碼合法（`ck_RoleGroupFeatures_Scope`、`ck_RoleGroupFeatures_ManagerScope`），「這個功能能用哪些範圍」由程式檢查。
- 兩組代碼與順序（由窄到寬）、各依據哪種單位、是否含下層，都宣告在 [`DataScopes`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/DataScopes.cs)；`IDataScope` 依宣告計算。
- 功能在 [`FeatureCatalog`](../../backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs) 以 `Scopes`、`ManagerScopes` 宣告支援的範圍（由窄到寬）。**宣告了就表示查詢已套用範圍**；還沒套用的功能不要宣告，否則管理員設了範圍卻沒有效果。
- 群組儲存（`PUT /api/v1/admin/groups/{id}` 的 `scopes`、`managerScopes`）：有宣告範圍的功能必須給一個支援的值；主管範圍可不給，給了就要是支援的值；沒宣告的不可帶。否則 `invalid_data_scope`（同樣留下稽核，快照含兩種範圍）。
- 管理介面每個有範圍的功能一列：「所有成員」與「主管另可看」（功能沒宣告主管範圍時不顯示）。新勾選的功能預設最窄的範圍、主管不另外；「所有成員」選全部時第二個選單停用並顯示「已含」。
- 目錄經 OpenAPI 的 `x-feature-catalog` 輸出每個功能的 `scopes`、`managerScopes` 與範圍名稱，前端的 `CATALOG_SCOPES` 由它產生。
- Seed 的預設授權也要符合宣告（`FeatureCatalogSeedTests`）。

## 讀取：`IDataScope`

[`IDataScope.ForAsync(userId, featureId)`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/IDataScope.cs) 回傳有效範圍：

- 各有效群組（角色、群組、功能都啟用）的範圍與主管範圍以 OR 合併；與功能授權同一次 SQL 讀取（`AccessService`），同一個 request 內不重讀授權。
- 目錄不支援的值（舊資料、目錄縮減）與 null 都不算，`IsEmpty`＝沒有資料：失敗即拒絕。
- `UnitIds`：`unit` 是使用者目前所屬的有效單位，`unit-tree` 再加上所有有效下層，再 OR 上[主管範圍](#主管範圍)的單位；有 `all` 時不計算。停用的單位連同下層、停用或刪除的帳號都不算。單位表與所屬各讀一次就算出四種範圍的單位，不隨範圍種類或單位數增加。
- 對沒有宣告範圍的功能呼叫會拋出（程式錯誤）。

單位由 AccessControl 定義的 [`IUnitMembership`](../../backend/src/AiNexus.Features/Admin/AccessControl/Scopes/IUnitMembership.cs) 計算，Organization 的 `OrganizationDirectory` 實作；AccessControl 不依賴 Organization（`ModuleBoundaryTests`）。

## 畫面

`/me` 與存取預覽的 `access.dataScopes` 列出每個已授權、有範圍的功能的有效代碼（`IDataScope.ScopesAsync`，含實際任主管時的主管範圍、不算單位），名稱取自 `CATALOG_SCOPES`。前端用 `WorkspaceSession.seesOthers(功能)` 在第一次渲染就決定版面：只看得到本人時不顯示選人、選單位，也不讀那些選單，而不是等名單讀回來再推論（會先閃出欄位再消失）。它只決定版面，資料仍由查詢依範圍篩選。

## 主管範圍

組織勾選為主管（`organization.UnitMembers.IsManager`）的人，在群組授權的主管範圍內另外看得到所管單位的資料，不必另建主管群組。理由與取捨見 [ADR 0022](../decisions/0022-manager-scopes-on-grants.md)。

- `managed` 是此人**任主管**的有效單位，`managed-tree` 再加有效下層；同時所屬但不是主管的單位不加入。取消勾選、單位停用、帳號停用都在下一個請求失效。
- [首長單位](../features/organization.md#首長單位)的主管另外任上層的主管（`managed` 加上層），`managed-tree` 的上層只展開層級不比它高的子單位（[ADR 0024](../decisions/0024-head-units.md)）；有首長單位的上層，自己的主管勾選不算。
- 不是主管的人，即使授權帶了主管範圍，有效範圍也不含 `managed*`；範圍已含 `all` 時不另外計算也不列出。
- 不授予功能本身：要先有該功能的授權（沒有仍是 403）；成員範圍沒有可用值時只靠主管範圍。模組照一般範圍處理 `UnitIds`，不需要分辨來源。
- 何時宣告 `ManagerScopes`：主管看部屬資料合乎業務（出勤）。主管不該多看的（稽核、對話）不宣告，管理員也就無法給。

## 在模組裡套用

1. 在目錄宣告該功能支援的範圍（需要時也宣告主管範圍），Seed 的預設授權給一個值（administrators 一律給宣告的最寬範圍、不帶主管範圍，見[預設授權](access-control.md#預設授權)），開發庫加變更腳本回填既有授權。
2. handler 取 `IDataScope`，自己把範圍轉成查詢條件（EF 的 `Where` 或預存程序參數），寫在查詢裡；不用 EF 全域查詢篩選（不知道是哪個功能在查，也能被 `IgnoreQueryFilters` 繞過）。
3. 業務資料寫入時有存單位 ID 的，直接比對 `UnitIds`；沒有存的，用 `OrganizationDirectory.MemberIds(unitIds)` 換成目前成員，它是可組合的子查詢，整段仍是一個 SQL。
4. `assigned` 的關係（專案成員、負責客戶）由業務模組自己判斷，平台只回答有沒有這個範圍。
5. 沒有範圍就沒有資料；單筆讀取查不到（含範圍外）一律 404，不透露資料存在。範圍不影響 policy：沒有功能授權仍是 403。
6. 整合測試至少涵蓋各宣告範圍、多群組 OR、沒有範圍與停用單位；宣告了主管範圍的再加主管情境（逐群組開關、非主管）。

## 目前套用的功能

| 功能 | 範圍 | 主管範圍 | 條件 |
|---|---|---|---|
| `audit`（[活動稽核](../features/activity-audit.md#權限)） | `own`、`unit`、`unit-tree`、`all` | 無 | 紀錄的操作者或對象是範圍內的人（`AuditDataScope`） |
| `attendance.records`（[出勤紀錄](../features/attendance-records.md#資料範圍)） | `own`、`unit`、`unit-tree`、`all` | `managed`、`managed-tree` | 持卡人是範圍內的人（`ListAttendanceRecords`） |
