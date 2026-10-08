# 身分、角色、群組與功能

系統日誌新增 `logs.query`、`logs.detail`、`logs.export` 三個獨立 feature，初始只授予 administrators group；detail／export 同時需要 query。這些 API 使用伺服器 FeatureRequirement，即使猜對查證代碼也不能繞過。`admin` 管理授權本身不替代明確的日誌 feature。查詢／詳情／匯出有獨立持久稽核與限制，見 [DIAGNOSTICS](DIAGNOSTICS.md#權限查詢與稽核可靠性)。

活動稽核使用獨立 `audit` feature 與 `/admin/audit` 頁面，初始授予 administrators；升級時為已有 admin grant 的群組補上 audit，保留既有查閱能力。可只授予稽核人員 audit 而不提供帳號管理權限，撤權在下一次請求生效。詳見 [活動稽核](ACTIVITY_AUDIT.md)。

AD 或本地密碼驗證決定「你是誰」，access schema 決定「你可以用哪些功能」，conversation owner 決定「你可讀寫哪筆資料」。兩種登入可綁定同一個平台 UserId；停用或移除使用者會排除所有 grant。取得 chat 功能也只能存取自己的對話，其他人的 ID 一律以 404 回應。

具有 `admin` 功能的管理員可透過獨立、會留下敏感讀取稽核的管理 API 檢視使用者用量及對話。此例外僅提供唯讀內容，沒有放寬一般 conversations 的 owner 檢查或附件下載 ACL；操作方式見 [管理工作區](ADMINISTRATION.md)。

## 第一版預設

即時監控使用獨立 monitoring feature，初始只授予 administrators。Presence 可由已驗證成員報送，監控讀取要求明確 grant；SSE 每十五秒重驗帳號與授權，撤銷後中止既有訂閱。見 [監控](MONITORING.md)。

每個首次登入的使用者取得 `member` 角色，加入 `workspace` 群組。最初版本只授予 `chat`；完整 migrations 後亦提供 files、projects、knowledge、artifacts、shared、quality、tasks。平台管理員可調整群組功能；admin／integrations 預設在獨立的 administrators 群組。三種主檔皆有 Enabled，停用會排除 grant。多個角色／群組的功能取聯集並去重，不採名稱或前端路由推斷權限。

模型授權也取有效群組聯集；任一群組未限制模型即可使用全部平台可用模型，個人白名單只能進一步限縮。逐模型 token 額度只合併有授權該模型的群組，取最高值、無上限優先，個人覆寫優先。停用群組或撤銷角色後，下一次請求重新計算授權與額度。詳見 [模型政策](ADMINISTRATION.md)。

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

角色、群組、使用者分派與功能啟用由 [管理工作區](ADMINISTRATION.md) 維護。`administrator → administrators → admin` 是獨立的管理授權；一般使用者無法呼叫其編輯 API。bootstrap 使用明確設定的 AD 帳號且只授權一次，撤銷不會因再次登入而補回。

## 受控查詢例子

```sql
SELECT u.[Id], u.[Account], r.[Id] AS [RoleId], r.[Name], r.[Enabled]
FROM [identity].[Users] u
LEFT JOIN [access].[UserRoles] ur ON ur.[UserId] = u.[Id]
LEFT JOIN [access].[Roles] r ON r.[Id] = ur.[RoleId];
```

管理時使用 UserId／RoleId／GroupId／FeatureId，避免依顯示名稱相連。Bridge tables 的外鍵與複合主鍵防孤兒／重複關聯。新增多筆授權與 audit 應同一 transaction 提交；資料庫物件說明見 [DATABASE](DATABASE.md)。
