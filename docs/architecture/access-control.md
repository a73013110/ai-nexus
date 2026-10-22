# 身分、角色、群組與功能

AD 或本地密碼驗證決定「你是誰」，access schema 決定「你可以用哪些功能」，資料的 owner 或授權上的[資料範圍](data-scopes.md)決定「你可讀寫哪筆資料」。兩種登入可綁定同一個平台 UserId；停用或移除使用者會排除所有 grant。取得 chat 功能也只能存取自己的對話，其他人的 ID 一律以 404 回應。

具有 `identity.users` 功能的管理員可透過獨立、會留下敏感讀取稽核的管理 API 檢視使用者用量及對話。此例外僅提供唯讀內容，沒有放寬一般 conversations 的 owner 檢查或附件下載 ACL；操作方式見 [存取管理](../features/administration.md)。

## 預設授權

每個首次登入的使用者取得 `member` 角色，加入 `workspace` 群組。內建功能與預設群組寫在 `database/App/Seed/accesscontrol.sql`：`workspace` 是工作台頁面與品質評測；`administrators`（平台管理）擁有全部功能、範圍取最寬（`FeatureCatalogSeedTests` 強制）；`hr` 是出勤管理（[出勤打卡](../features/attendance.md)），獨立成群組讓 HR 不必是管理員。預設授權只寫進內建群組；新增的功能不會自動加到既有的自訂群組。三種主檔皆有 Enabled，停用會排除 grant。多個角色／群組的功能取聯集並去重，不採名稱或前端路由推斷權限。

## 功能目錄

頁面、動作與閱讀器只定義在 [`FeatureCatalog`](../../backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs)：ID、區、模組、預設名稱、圖示，網址由區＋ID 推得（`identity.users` → `/admin/identity/users`）。規則與原因見 [ADR 0016](../decisions/0016-areas-urls-and-navigation.md)。

- **ID**：`<module>.<page>[.<action>]`，模組 kebab-case，不帶區；模組本身就是單一頁面時 ID＝模組。動作只是授權、沒有網址；閱讀器只是路由，能否讀取由資源 ACL 決定，不是授權。
- **資料庫**：`accesscontrol.Features` 只存可授予的功能（頁面＋動作）、Enabled 與名稱覆寫（null＝目錄預設）。`FeatureCatalogSeedTests` 在範本資料庫上雙向比對目錄與 Seed；資料庫有、目錄不認得的列仍是授權，只是不出現在選單。功能清單（`/me`、管理與稽核目錄）一律依目錄的宣告順序。
- **前端**：OpenAPI 以目錄的授權輸出 `FeatureId` 列舉，並在文件根的 `x-feature-catalog` 輸出整份目錄；`npm run contracts` 由它產生 `feature-catalog.ts`。路由、守衛、麵包屑與預設名稱都從這裡來（[前端邊界](../frontend/frontend-boundaries.md)）。
- **後端產生的前端連結**（總覽、通知）用 `FeatureCatalog.RouteOf`，不寫死路徑。
- 角色、群組、預設選單分群節點與預設關聯的 ID 在 `BuiltInAccess`。測試身分不可寫入的管理功能列在 `AdministrativeFeatures`（`Policies.cs`）。

模型授權與逐模型額度也由有效群組合併，下一次請求重新計算，見 [模型政策與配額](../features/model-policy.md)。

```text
AD login → Users（SID）→ UserRoles → Roles
                                  → RoleGroupRoles → RoleGroups
                                                   → RoleGroupFeatures（含範圍）→ Features
```

登入後前端自動呼叫 `/api/v1/me`，收到 `access.roles`、`access.groups`、`access.features` 與 `access.navigation`（已修剪的[選單](#選單)樹），以此顯示可用入口與帳號授權資訊。沒有 chat 的使用者仍能取得身分與偏好，聊天送出停用。

後端 conversations、models、context、runs、取消與 SSE 全部要求 `feature:chat` policy。AuthorizationHandler 每個 request 查 SQL 的有效 grant，同一 request 內的授權處理、資源 ACL 與模型政策共用這次讀取（管理異動與遠端呼叫後的複查會重新讀取）；撤銷在下一次 request 生效，不等待 cookie 過期。已建立的生成會按原工作生命週期完成；已開啟的 SSE 訂閱不持續重驗授權。要立即中斷既有工作／訂閱需另增加撤銷事件機制。

首次登入建立 UserRole；既有使用者沒有角色時不自動補回，避免管理員撤銷後，使用者重新登入又獲得權限。

## 登入身分

- 目前每個帳號只有「個人」身分，角色是權限聯集；`AuthSessionDto.identities` 固定一筆，登入頁因此跳過身分選擇。
- 擴充點：之後的組織、部門或代理人身分加在 `AuthSession.Identities`，選擇由 `POST /api/v1/auth/identity` 寫入 cookie（`active_identity`）。加入第二種身分時，同一個變更要決定該身分的授權範圍（`AccessService`）並寫稽核；只顯示選項而不改授權會讓使用者誤以為權限已切換。

## 選單

側欄的樹是**資料**（`accesscontrol.NavigationItems`），由管理員在[存取管理](../features/administration.md#選單)調整；與程式資料夾、網址、麵包屑無關，也不授予任何功能（[ADR 0016](../decisions/0016-areas-urls-and-navigation.md#選單)）。

- **節點**：`group` 分群（可巢狀，名稱與圖示可改）、`page` 指向目錄的頁面（名稱取功能的名稱覆寫或目錄預設，圖示取目錄）、`link` 外部連結（名稱、網址、是否另開分頁）。同層依 `SortOrder`、再依 Id。
- **結構規則**（`NavigationRules`，套在變更後的整棵樹上）：父節點必須是分群、不可成環、最多 4 層、同一層不可重複同一頁面（不同位置可以）；page 必須是目錄的頁面。批次移動全部通過才寫入；分群要先清空才能刪除。
- **可見範圍**：`/me` 與存取預覽回傳伺服器修剪過的樹（`NavigationTree.Build`）。page 要已授權且啟用；link 要沒有指定可見群組，或使用者屬於其中任一有效群組（`NavigationItemAudiences`），且主機仍在允許清單；沒有可見子節點的分群不輸出（逐層判斷），只剩一個也保留。授權檢查（`AccessService.ForUserAsync`）不讀選單。管理與稽核目錄另附未修剪樹上的功能位置（`NavigationTree.Sections`）供授權清單分組。
- **外部連結**：網址須以 `https://` 開頭、不含使用者資訊，主機須在 `AccessControl:NavigationLinkHosts` 允許清單內（[寫法](../operations/iis-configuration.md#外部連結允許清單)；預設空＝不允許）。儲存與產生 `/me` 時各檢查一次（`NavigationLinks`），主機移出清單後連結直接不輸出、不報錯。
- **Seed**：`database/App/Seed/accesscontrol.sql` 寫入預設樹（Id 是可讀代稱，如 `work.chat`），只新增、不覆蓋：以 Id 比對，移動過的節點留在原位；刪除的預設節點只要上層還在，下次 Seed 會再加回來。`FeatureCatalogSeedTests` 要求目錄每個頁面至少放在預設樹一次。

## 擴充功能

1. 在所屬區新增 `Features/Work/Reports` 與 `ReportsModule.cs`；在 `FeatureCatalog` 加模組與頁面（例如 `reports`），在 `database/App/Seed/accesscontrol.sql` 加功能列（Name 為 NULL）、預設授權，以及預設選單樹的 page 節點（`FeatureCatalogSeedTests` 檢查）。
2. 在 `Policies` 加 policy，於 `ReportsModule` 註冊 `services.AddFeaturePolicy(...)`、加入 `FeatureModules` 清單，endpoint 明確 RequireAuthorization。
3. 重產合約（`FeatureId` 與 `feature-catalog.ts` 隨之更新），前端新增 `reports.routes.ts`，路由以 `data: { feature }` 指向目錄；UI 判斷只改善體驗，API policy 仍是實際權限邊界。
4. 有資料列、要依部門切資料的功能宣告範圍並套用（[資料範圍](data-scopes.md)）。
5. 以至少兩個角色驗沒有 grant 的 403、資料 owner 或範圍外的 404 與停用／撤銷，並更新文件。

`administrator → administrators` 是獨立的管理授權；一般使用者無法呼叫其編輯 API。第一位管理員見 [存取管理](../features/administration.md#設定第一位管理員)。

管理時使用 ID，不依顯示名稱相連；授權與 audit 同一 transaction 提交。
