# 身分、角色、群組與功能

AD 或本地密碼驗證決定「你是誰」，access schema 決定「你可以用哪些功能」，conversation owner 決定「你可讀寫哪筆資料」。兩種登入可綁定同一個平台 UserId；停用或移除使用者會排除所有 grant。取得 chat 功能也只能存取自己的對話，其他人的 ID 一律以 404 回應。

具有 `admin` 功能的管理員可透過獨立、會留下敏感讀取稽核的管理 API 檢視使用者用量及對話。此例外僅提供唯讀內容，沒有放寬一般 conversations 的 owner 檢查或附件下載 ACL；操作方式見 [管理工作區](../features/ADMINISTRATION.md)。

## 預設授權

每個首次登入的使用者取得 `member` 角色，加入 `workspace` 群組。各模組以 `FeatureSeed` 宣告自己的功能與預設群組：`workspace` 預設有 dashboard、chat、files、projects、knowledge、artifacts、shared、quality、repositories、tasks；`administrators` 預設有 admin、integrations、monitoring、audit 與 `logs.query`／`logs.detail`／`logs.export`。`admin` 不隱含其他管理功能，monitoring、audit、logs 都要明確授予（見 [活動稽核](../features/ACTIVITY_AUDIT.md)、[系統日誌](../features/SYSTEM_LOGS.md)、[監控](../features/MONITORING.md)）。平台管理員可調整群組功能。三種主檔皆有 Enabled，停用會排除 grant。多個角色／群組的功能取聯集並去重，不採名稱或前端路由推斷權限。

模型授權也取有效群組聯集；任一群組未限制模型即可使用全部平台可用模型，個人白名單只能進一步限縮。逐模型 token 額度只合併有授權該模型的群組，取最高值、無上限優先，個人覆寫優先。停用群組或撤銷角色後，下一次請求重新計算授權與額度。詳見 [模型政策與配額](../features/MODEL_POLICY.md)。

```text
AD login → Users（SID）→ UserRoles → Roles
                                  → RoleGroupRoles → RoleGroups
                                                   → RoleGroupFeatures → Features
```

登入後前端自動呼叫 `/api/v1/me`，收到 `access.roles`、`access.groups`、`access.features`，以此顯示可用入口與帳號授權資訊。沒有 chat 的使用者仍能取得身分與偏好，聊天送出停用。

後端 conversations、models、context、runs、取消與 SSE 全部要求 `feature:chat` policy。AuthorizationHandler 每個 request 查 SQL 的有效 grant，同一 request 內的授權處理、資源 ACL 與模型政策共用這次讀取（管理異動與遠端呼叫後的複查會重新讀取）；撤銷在下一次 request 生效，不等待 cookie 過期。已建立的生成會按原工作生命週期完成；已開啟的 SSE 訂閱不持續重驗授權。要立即中斷既有工作／訂閱需另增加撤銷事件機制。

首次登入建立 UserRole；既有使用者沒有角色時不自動補回，避免管理員撤銷後，使用者重新登入又獲得權限。

## 擴充功能

1. 新增 `Features/Reports` 與 `ReportsModule.cs`，宣告 `FeatureSeed` 子類別，列出穩定 Feature ID（例如 `reports`）、名稱、route、SortOrder 與是否只給管理員，再產生 migration。
2. 在 `ReportsModule` 註冊服務與 `services.AddFeaturePolicy("reports")`，加入 `FeatureModules` 清單，endpoint 明確 RequireAuthorization。
3. 前端新增 lazy route／feature，根據 `/me` 的 features 顯示入口；UI 判斷只改善體驗，API policy 仍是實際權限邊界。
4. 以至少兩個角色驗沒有 grant 的 403、資料 owner 的 404 與停用／撤銷。重產 OpenAPI 並更新文件。

角色、群組、使用者分派與功能啟用由 [管理工作區](../features/ADMINISTRATION.md) 維護。`administrator → administrators → admin` 是獨立的管理授權；一般使用者無法呼叫其編輯 API。bootstrap 使用明確設定的 AD 帳號且只授權一次，撤銷不會因再次登入而補回。

## 受控查詢例子

```sql
SELECT u.[Id], u.[Account], r.[Id] AS [RoleId], r.[Name], r.[Enabled]
FROM [identity].[Users] u
LEFT JOIN [accesscontrol].[UserRoles] ur ON ur.[UserId] = u.[Id]
LEFT JOIN [accesscontrol].[Roles] r ON r.[Id] = ur.[RoleId];
```

管理時使用 UserId／RoleId／GroupId／FeatureId，避免依顯示名稱相連。Bridge tables 的外鍵與複合主鍵防孤兒／重複關聯。新增多筆授權與 audit 應同一 transaction 提交；資料庫物件說明見 [DATABASE](DATABASE.md)。
