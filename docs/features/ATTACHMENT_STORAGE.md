# 附件保存、容量與刪除

- 資料庫只存 metadata 與引用，原檔放在站外目錄；同一份原檔只存一次、只計一次容量。
- 上傳、引用、刪除都在 SQL 交易與使用者 row lock 內重新檢查，檔案 IO 不持有交易。
- 完整備份必須包含 SQL 與同一時點的附件目錄，見 [備份與還原](../operations/BACKUP.md)。格式與上限見 [文件與圖片分析](ATTACHMENTS.md)。

## 資料與儲存

- `attachments.Attachments` 保存 owner、metadata、StorageKey、StorageState、抽取文字與引用所需資料，不存原始 bytes。`MessageAttachments`／`ResourceAttachments` 連接提問、知識庫、專案與分享。重新上傳相同內容仍是新的原檔，不依 hash 跨使用者共用。
- `IAttachmentStorage` 把 domain 與實體儲存分開。`Attachments.StoragePath` 必須是站外絕對路徑；Development 留空時用 `.local/data/attachments`，IIS 可用 `Attachments__StoragePath` 覆寫。啟動時拒絕相對路徑或網站目錄內的路徑。
- 原檔以不可猜測的 32 字元 StorageKey 保存為 `<前2碼>/<次2碼>/<StorageKey>.blob`，原始檔名只是 metadata。目錄不映射 IIS 虛擬目錄、沒有靜態 URL。
- 下載要通過 owner、知識／專案 ACL 或有效的具名分享，才以 stream 回傳。圖片仍檢查格式標記，不接受 SVG。

## 上傳流程

1. 驗證格式與實際長度。
2. 交易內以使用者 SQL row lock 檢查容量，建立 `pending` 預約（這把鎖也能協調共用同一個 SQL 的不同程序）。
3. commit 後寫入 `.upload`，flush 並原子 rename 成 `.blob`，最後轉為 `ready`。只有 `ready` 的檔案能建立引用或下載。

失敗以 `deleting` outbox 清理；預約已被回收才寫完檔時，重新建立清理記錄與容量。程序在寫檔與 SQL 確認之間中斷時，由定期孤兒掃描接手。空間不足回 413／`attachment_quota`。

## 容量

- 用量是每份原檔 Size 的總和，包含草稿、`pending` 與尚未刪除成功的 `deleting`；不加總引用，也不計衍生文字與索引。
- 有效上限：個人 `identity.Users.AttachmentLimitBytes` → 有效群組的最低非空上限 → 網站預設。個人上限可超過群組與預設；0 禁止新上傳；降低上限不刪既有檔案，剩餘最低為 0。
- 個人設定、檔案庫與管理介面顯示已用／上限／剩餘；API 是 `GET /api/v1/attachments/storage` 與 `PUT /api/v1/admin/users/{id}/storage`（見 [模型政策與配額](MODEL_POLICY.md#附件容量)）。

## 引用與刪除

- 歷史只載入 metadata；Context 預覽只讀文字與圖片預估。附件關聯、提問與 run 在同一筆交易建立。
- 成功送出、加入知識庫／專案或從檔案庫上傳後標記 `InLibrary`。刪除對話只移除訊息引用（訊息本身是軟刪除），原檔仍可在檔案庫重用；從輸入框移除已保存的檔案也不刪原檔。
- 要釋放空間，先移除對話、知識庫、專案與分享的引用，再從檔案庫明確刪除；伺服器在交易與 owner lock 內重新檢查引用，仍被引用時回 409。
- 實際刪檔是 durable outbox：移除引用並標記 `deleting`，commit 後刪 `.blob` 與 `.upload`，成功才刪 metadata、釋放容量。檔案不存在視為已刪；磁碟或權限失敗保留記錄與容量，下一週期重試。

## 背景清理

`AttachmentCleanupWorker` 在啟動時與每 `Attachments.CleanupIntervalMinutes`（預設 60 分鐘）執行：

- 未保存且沒有 durable 引用的 `ready` 草稿超過 `DraftRetentionDays`（預設 14 天）回收，私人閱讀器的衍生頁面一併移除；中斷的 `pending` 一小時後回收；每批最多 100 筆。
- 上傳時只先回收上傳者本人的過期草稿，避免占用即將檢查的容量。
- 串流掃描儲存區，只處理標準命名、最後寫入超過 24 小時且 SQL 沒有 metadata 的孤兒 `.blob`／`.upload`，每 100 個識別批次核對。
- 維運要監控清理警告與磁碟剩餘量；每人上限不等於磁碟總容量。清除瀏覽器草稿不等於伺服器刪檔。

## 文字備份與副本

JSON 文字備份保存分支、指令、標籤與附件名稱，**不含原檔**，匯入後要重新上傳；「建立對話副本」完整保留附件關聯。
