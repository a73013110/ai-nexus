# 平台管理

管理工作區在 `/admin`，只有有效權限包含 `admin` 的使用者可用；一般登入不會取得管理權。API 每次查詢與異動都重新驗證，寫入另要求 CSRF。相關頁面：[模型政策與配額](MODEL_POLICY.md)、[測試身分](TEST_IDENTITY.md)、[活動稽核](ACTIVITY_AUDIT.md)、[系統日誌](SYSTEM_LOGS.md)、[即時監控](MONITORING.md)。授權模型見 [身分與授權](../architecture/ACCESS_CONTROL.md)。

## 設定第一位管理員

在本機 `.local/config/appsettings.Local.json` 或正式主機的一般設定加入：

```json
{
  "Administration": {
    "BootstrapAdministrators": ["it.admin"]
  }
}
```

填 AD 短帳號（不含密碼、DN 或網域前綴）後重啟。該帳號下一次通過 AD 驗證時取得 `administrator` 角色，並在 `administration.AdministratorBootstraps` 留下唯一標記與稽核。移除角色後不會因再次登入而補回；正式上線後可清空清單，改由管理後台授權。

## 使用者

- 支援新增、編輯、停用與移除。每位使用者可啟用 AD、本地密碼或兩者，兩種登入對應同一個 UserId、角色與私人資料。
- 預建的 AD 帳號填目錄短帳號，首次成功驗證才綁定 SID；已綁定的帳號不能換綁他人。手動輸入的姓名不會被目錄登入覆蓋。
- 移除是邏輯刪除，保留歷史對話、稽核與登入名稱，防止重建同名帳號接管資料。
- 本地密碼採 Argon2id PHC 格式（64 MiB、3 iterations、parallelism 1、128-bit salt），API、前端與稽核只顯示「是否已設定」。密碼 12–128 字元；登入有 IP 限流，連續 5 次失敗暫鎖 15 分鐘，管理員重設密碼可解除。本地姓名與 bootstrap 帳號相同也不會取得管理權。
- 停用、修改登入政策或重設密碼會撤銷舊 cookie。管理員不能移除自己、停用自己目前的登入方式或撤銷自己的管理權。

## 授權工作流程

1. 使用者首次登入建立平台身分與 `member` 角色（`workspace` 群組）。
2. 「功能群組與模型」新增群組，勾選功能，視需要限制模型與配額。
3. 「角色」建立工作角色並加入群組。
4. 「使用者」搜尋帳號並勾選角色；儲存前可預覽可用功能，儲存後下一次 API 操作生效。

- 主檔用穩定 ID，名稱可改。角色與群組只能停用、不刪除，保留授權歷史；功能由各模組的 `FeatureSeed` 註冊，管理員只調整名稱、順序與啟用狀態。
- 異動在同一個 serializable 交易保存授權與稽核。會撤銷操作者自身管理權的變更回 409 並回滾，交由另一位管理員執行。異動 JSON 只含授權、模型識別與限制。
- 功能清單、群組授權與存取預覽和側欄共用 `frontend/src/app/core/feature-groups.ts` 的分類（工作、協作與品質、系統）；未分類的新功能放在「更多工具」。名稱、排序與啟用狀態都來自資料庫的功能目錄。

## 使用者用量與對話

- 使用者列表顯示最近 30 天的生成次數、回報 tokens、對話數與原檔容量。點姓名開啟活動視窗，分為「對話」「AI 模型」「附件容量」。
- 用量由共用的 `UsageReports` 統計 `GenerationRuns` 與 `ModelInvocations`，涵蓋聊天、OCR、文字工具、評測與耗時；期間是最近 30 個 UTC 日期，顯示用台北時區，未回報的 token 不估算。
- 每次回答可展開總耗時、排隊、執行與輸入／輸出 tokens；執行時間包含 Context 準備與等待 provider 容量，不是供應商的純運算時間。
- 活動視窗可搜尋標題與本文，包含封存與已軟刪除的對話；對話每頁 50 段、內容每次 100 則，顯示資料庫保存的所有分支。附件只顯示名稱與大小，不增加下載權限。
- 唯讀 API：`GET /api/v1/admin/users/{id}/insights`、`.../users/{id}/conversations`、`/api/v1/admin/conversations/{id}`。每次重新檢查 `admin`，回應前先保存 `admin.user_usage_read`、`admin.conversations_list` 或 `admin.conversation_read` 稽核。管理員不能透過一般 API 修改他人的對話；登入頁會說明這項授權檢視。

## 平台用量與費用

管理工具的平台用量看最近 30 天的活躍使用者、完成／失敗／取消、各模型輸入輸出、缺失用量與原檔容量，另顯示模型供應商連線與搜尋設定。區間費用、價格版本與 CSV 在總覽的平台範圍，見 [費用](BILLING.md)；平台讀取與匯出都會留下稽核。
