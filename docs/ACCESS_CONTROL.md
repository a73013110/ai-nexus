# 身分、角色、群組與功能

AD 驗證決定「你是誰」，access schema 決定「你可以用哪些功能」，conversation owner 決定「你可讀寫哪筆資料」。取得 chat 功能也只能存取自己的對話，其他人的 ID 一律以 404 回應。

## 第一版預設

每個首次登入的使用者取得 `member` 角色，加入 `workspace` 群組。最初版本只授予 `chat`；完整 migrations 後亦提供 projects、knowledge、artifacts、shared、quality、tasks。平台管理員可調整群組功能；admin／integrations 預設在獨立的 administrators 群組。三種主檔皆有 Enabled，停用會排除 grant。多個角色／群組的功能取聯集並去重，不採名稱或前端路由推斷權限。

```text
AD login → Users（SID）→ UserRoles → Roles
                                  → RoleGroupRoles → RoleGroups
                                                   → RoleGroupFeatures → Features
```

登入後前端自動呼叫 `/api/v1/me`，收到 `access.roles`、`access.groups`、`access.features`，以此顯示可用入口與帳號授權資訊。沒有 chat 的使用者仍能取得身分與偏好，聊天送出停用。

後端 conversations、models、context、runs、取消與 SSE 全部要求 `feature:chat` policy。AuthorizationHandler 每個 request 查 SQL 的有效 grant；撤銷在下一次 request 生效，不等待 cookie 過期。已建立的生成會按原工作生命週期完成；已開啟的 SSE 訂閱不持續重驗授權。要立即中斷既有工作／訂閱需另增加撤銷事件機制。

首次登入建立 UserRole；既有使用者沒有角色時不自動補回。migration 只在升級當次 backfill。避免管理員撤銷後，使用者重新登入又獲得權限。

## 擴充功能

1. 在新 migration 加入穩定 Feature ID（例如 `reports`）、名稱、route 與 SortOrder，及管理員希望使用的 RoleGroupFeatures 關聯。
2. 新增 `Modules/Reports`，在 host 註冊需要的服務與 `FeatureRequirement("reports")` policy，endpoint 明確 RequireAuthorization。
3. 前端新增 lazy route／feature，根據 `/me` 的 features 顯示入口；UI 判斷只改善體驗，API policy 仍是實際權限邊界。
4. 以至少兩個角色驗沒有 grant 的 403、資料 owner 的 404 與停用／撤銷。更新 migration SQL、OpenAPI、文件。

角色、群組、使用者分派與功能啟用由 [管理工作台](ADMINISTRATION.md) 維護。`administrator → administrators → admin` 是獨立的管理授權；一般使用者無法呼叫其編輯 API。bootstrap 使用明確設定的 AD 帳號且只授權一次，撤銷不會因再次登入而補回。

## 受控查詢例子

```sql
SELECT u.[Id], u.[Account], r.[Id] AS [RoleId], r.[Name], r.[Enabled]
FROM [identity].[Users] u
LEFT JOIN [access].[UserRoles] ur ON ur.[UserId] = u.[Id]
LEFT JOIN [access].[Roles] r ON r.[Id] = ur.[RoleId];
```

管理時使用 UserId／RoleId／GroupId／FeatureId，避免依顯示名稱相連。Bridge tables 的外鍵與複合主鍵防孤兒／重複關聯。新增多筆授權與 audit 應同一 transaction 提交；資料庫物件說明見 [DATABASE](DATABASE.md)。
