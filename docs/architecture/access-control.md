# 身分、角色、群組與功能

AD 或本地密碼驗證決定「你是誰」，access schema 決定「你可以用哪些功能」，conversation owner 決定「你可讀寫哪筆資料」。兩種登入可綁定同一個平台 UserId；停用或移除使用者會排除所有 grant。取得 chat 功能也只能存取自己的對話，其他人的 ID 一律以 404 回應。

具有 `admin` 功能的管理員可透過獨立、會留下敏感讀取稽核的管理 API 檢視使用者用量及對話。此例外僅提供唯讀內容，沒有放寬一般 conversations 的 owner 檢查或附件下載 ACL；操作方式見 [存取管理](../features/administration.md)。

## 預設授權

每個首次登入的使用者取得 `member` 角色，加入 `workspace` 群組。內建功能與預設群組寫在 `database/App/Seed/accesscontrol.sql`：`workspace` 預設有 dashboard、chat、files、projects、knowledge、artifacts、shared、quality、repositories、tasks；`administrators` 預設有 admin、operations、integrations、monitoring、audit、design 與 `logs.query`／`logs.detail`／`logs.export`。`admin` 不隱含其他管理功能，operations、monitoring、audit、logs 都要明確授予（見 [平台營運](../features/operations.md)、[活動稽核](../features/activity-audit.md)、[系統日誌](../features/system-logs.md)、[監控](../features/monitoring.md)）。預設授權只寫進內建群組；新增的功能不會自動加到既有的自訂群組，需要時由管理員手動授予。管理員可調整群組功能。三種主檔皆有 Enabled，停用會排除 grant。多個角色／群組的功能取聯集並去重，不採名稱或前端路由推斷權限。

程式依賴的 ID 各只定義一次：角色、群組、選單分群與預設關聯在 `BuiltInAccess`，功能在 `FeatureIds`（模組的常數引用它）。`BuiltInAccessSeedTests` 在範本資料庫上雙向比對 Seed：程式要的列都在、Seed 的功能程式都認得。OpenAPI 以 `FeatureIds.All` 輸出 `FeatureId` 列舉，前端 `session.has()`、`FEATURE_NAMES` 以它為型別，打錯字無法編譯。

模型授權也取有效群組聯集；任一群組未限制模型即可使用全部平台可用模型，個人白名單只能進一步限縮。逐模型 token 額度只合併有授權該模型的群組，取最高值、無上限優先，個人覆寫優先。停用群組或撤銷角色後，下一次請求重新計算授權與額度。詳見 [模型政策與配額](../features/model-policy.md)。

```text
AD login → Users（SID）→ UserRoles → Roles
                                  → RoleGroupRoles → RoleGroups
                                                   → RoleGroupFeatures → Features
```

登入後前端自動呼叫 `/api/v1/me`，收到 `access.roles`、`access.groups`、`access.features` 與 `access.navigationGroups`，以此顯示可用入口與帳號授權資訊。沒有 chat 的使用者仍能取得身分與偏好，聊天送出停用。

後端 conversations、models、context、runs、取消與 SSE 全部要求 `feature:chat` policy。AuthorizationHandler 每個 request 查 SQL 的有效 grant，同一 request 內的授權處理、資源 ACL 與模型政策共用這次讀取（管理異動與遠端呼叫後的複查會重新讀取）；撤銷在下一次 request 生效，不等待 cookie 過期。已建立的生成會按原工作生命週期完成；已開啟的 SSE 訂閱不持續重驗授權。要立即中斷既有工作／訂閱需另增加撤銷事件機制。

首次登入建立 UserRole；既有使用者沒有角色時不自動補回，避免管理員撤銷後，使用者重新登入又獲得權限。

## 登入身分

- 目前每個帳號只有「個人」身分，角色是權限聯集；`AuthSessionDto.identities` 固定一筆，登入頁因此跳過身分選擇。
- 擴充點：之後的組織、部門或代理人身分加在 `AuthSession.Identities`，選擇由 `POST /api/v1/auth/identity` 寫入 cookie（`active_identity`）。加入第二種身分時，同一個變更要決定該身分的授權範圍（`AccessService`）並寫稽核；只顯示選項而不改授權會讓使用者誤以為權限已切換。

## 選單分群

結論：側欄怎麼分群是**資料**，由管理員在[存取管理](../features/administration.md#選單分群與順序)調整；程式碼資料夾跟著後端模組走（[前端邊界](../frontend/frontend-boundaries.md#功能資料夾對應後端模組)）。兩者刻意互不相關：重排選單不搬程式，拆模組也不改選單（原因見 [ADR 0009](../decisions/0009-module-names-and-data-driven-navigation.md)）。

- `accesscontrol.NavigationGroups` 是可巢狀的選單分群（`ParentId`），只決定排列、不授予任何功能；`Features.GroupId` 為 null 時位於最上層，`Features.Icon` 是前端圖示名稱（未知名稱改用預設圖示）。分群刪除前必須清空，外鍵都是 Restrict。
- 排序規則：每一層先列直屬的功能、再列子分群，各自依 `SortOrder`、再依 ID。內建分群 `work`（AI 工作台）、`collaboration`（協作）、`system`（系統管理）由同一份 Seed 腳本寫入；`dashboard` 在最上層。
- `/me` 與管理端的存取預覽只回傳含有可見功能（已授權、啟用、有 route）的分群及其上層，由 `NavigationTree.VisibleGroups` 計算；只授權不顯示的功能（route 為空，如 `logs.export`）不會讓分群出現。一般授權檢查（`AccessService.ForUserAsync`）不讀分群。
- 種子只決定預設值：Seed 腳本只新增缺少的列，管理員之後的調整不會被覆蓋。要改既有環境的值，另寫一次性的資料腳本。

## 擴充功能

1. 新增 `Features/Reports` 與 `ReportsModule.cs`，在 `FeatureIds` 加 `Reports = "reports"`；在 `database/App/Seed/accesscontrol.sql` 加功能列與預設授權：穩定 Feature ID（例如 `reports`）、名稱、route、SortOrder、預設選單分群（`work` 等，或 NULL 放最上層）、圖示名稱，以及授權給 `workspace` 或 `administrators`。需要新分群的模組在同一份腳本加自己的分群；例如之後的 Workforce 模組（打卡、請假）會種入 `workforce`「員工服務」，在那之前不預先建立空分群（[ADR 0009](../decisions/0009-module-names-and-data-driven-navigation.md)）。
2. 在 `ReportsModule` 註冊服務與 `services.AddFeaturePolicy(FeatureIds.Reports)`，加入 `FeatureModules` 清單，endpoint 明確 RequireAuthorization。
3. 前端新增 lazy route／feature，根據 `/me` 的 features 顯示入口，沒有授權時頁面顯示 `FeatureAccessNotice`（錯誤代碼見 [錯誤回應](error-contract.md#錯誤回應)）；UI 判斷只改善體驗，API policy 仍是實際權限邊界。
4. 以至少兩個角色驗沒有 grant 的 403、資料 owner 的 404 與停用／撤銷。重產 OpenAPI（前端 `FeatureId` 隨之更新，`FEATURE_NAMES` 補預設名稱）並更新文件。

角色、群組、使用者分派與功能啟用由 [管理工作區](../features/administration.md) 維護。`administrator → administrators → admin` 是獨立的管理授權；一般使用者無法呼叫其編輯 API。bootstrap 使用明確設定的 AD 帳號且只授權一次，撤銷不會因再次登入而補回。

## 受控查詢例子

```sql
SELECT u.[Id], u.[Account], r.[Id] AS [RoleId], r.[Name], r.[Enabled]
FROM [identity].[Users] u
LEFT JOIN [accesscontrol].[UserRoles] ur ON ur.[UserId] = u.[Id]
LEFT JOIN [accesscontrol].[Roles] r ON r.[Id] = ur.[RoleId];
```

管理時使用 UserId／RoleId／GroupId／FeatureId，避免依顯示名稱相連。Bridge tables 的外鍵與複合主鍵防孤兒／重複關聯。新增多筆授權與 audit 應同一 transaction 提交；資料庫物件說明見 [DATABASE](database.md)。
