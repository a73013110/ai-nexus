# 工作區通知規格 v1

通知屬於登入者，獨立於功能頁。側欄鈴鐺、未讀數、通知中心與瀏覽器通知共用 `NotificationStore`；事件保存於 `operations.Notifications`，重新整理或重新登入後仍可閱讀。通知不是授權，目標 API 必須重新檢查功能、owner、來源 ACL、分享期限或 Gitea 存取權。

所有側欄只有 heading 的鈴鐺入口，位於 sidebar-toggle 左側；未讀使用共用 CountBadge 的 info 色與 overlay，0 隱藏，超過 99 顯示 99+。按鈕保留「通知」名稱並描述完整數量，單一 atomic status 播報更新。未讀數來自 API 的全通知匣 `unread`，不從最近 50 筆估算嚴重性；事件的 info／success／error 在通知中心分別使用 info／success／danger 語意色。

## 公開契約

時間為 ISO 8601 UTC，識別使用 GUID；OpenAPI 與 generated schema 為型別來源。

| 欄位 | 規格 |
| --- | --- |
| `id` | 唯一 GUID；用於追蹤列、去重及瀏覽器 tag |
| `version` | 現為 `1`；未知版本只顯示文字及已讀／移除操作 |
| `type` | 穩定事件名稱，最多 80 字元；不用翻譯文字或 UI 路徑作名稱 |
| `severity` | `info`、`success`、`error`；另有圖示與可讀標題 |
| `title` | 純文字，最多 180 字元 |
| `body` | 純文字摘要，最多 600 字元；不保存密鑰、diff、完整回答或文件正文 |
| `target` | `{ kind, id }`；只允許登錄的內部資源，不接收任意 URL |
| `createdAt` | 建立時間，排序及全部已讀的範圍 |
| `readAt` | 已讀時間，未讀為 `null` |

內部欄位 `ownerId`、`eventKey`、`dismissedAt` 不輸出。`(ownerId, eventKey)` 唯一，重送不得重複通知。已讀／移除冪等且 owner scoped，其他帳號猜到 ID 仍不能操作。移除隱藏通知並保留去重資料；目前不自動刪除歷史。增加保留期限時須同時定義事件重播及去重期限。

| 目標 | 前端位置 | 重新授權 |
| --- | --- | --- |
| `conversation` | `/chat/{id}` | 對話 owner 與 chat 功能 |
| `task` | `/tasks?job={id}` | 任務 owner 與 tasks 功能；聚焦任務，即使不在最新 100 筆 |
| `share` | `/shared/{id}` | 收件者／寄件者、來源功能、期限、撤銷狀態 |
| `repository-review` | `/repositories?review={id}` | Review owner、repositories 功能、目前 Gitea 權限 |

`notification-target.ts` 是唯一 URL 轉換邊界，拒絕未知種類／版本及非法 ID。點擊先保存已讀，再關閉通知與手機側欄並跳轉。背景任務與 review 使用共用 `ResourceTarget` 定位與移動鍵盤焦點，僅在指定資源首次出現時執行，輪詢不重設閱讀位置。目標失效由功能頁顯示錯誤，不得回退成其他資源內容。

## 事件與提交

| 事件 | 去重 key | 目標 |
| --- | --- | --- |
| `conversation.completed`／`conversation.failed`／`conversation.cancelled` | `run:{runId}` | conversation |
| `task.completed`／`task.failed`／`task.cancelled` | `job:{jobId}:{attempt}` | task；程式碼 review 使用 repository-review |
| `share.received` | `share:{shareId}`，每位收件者各一則 | share |

通知與來源狀態使用同一 transaction。`PublishAsync` 加入 tracked entity，由呼叫者 SaveChanges／commit。Job 結束先丟棄尚未 checkpoint 的 tracked 修改，再以租約 token 限制 terminal update，原子提交通知。失去租約或 host shutdown 不發布錯誤完成事件；重試的每個 attempt 可有獨立通知。

對話 spinner 來自 app 根層 ChatStore 的 live run，切換對話仍繼續。未讀的 `conversation.completed` 顯示完成圖示，開啟對話後標為已讀。

## API、分頁及同步

前綴 `/api/v1`，需要登入；寫入沿用 CSRF。

| 方法與路徑 | 行為 |
| --- | --- |
| `GET /notifications?unread=false&before={id}` | 每頁最多 50 筆，回 `{ items, unread, hasMore }`；unread 是全通知匣未讀數 |
| `POST /notifications/{id}/read` | 單則已讀，204 |
| `POST /notifications/read`，body `{ through }` | 將 `createdAt <= through` 標為已讀；使用清單最新時間，不清除較晚事件 |
| `DELETE /notifications/{id}` | 從清單及未讀數隱藏，204 |

排序 `createdAt DESC, id DESC`，cursor 同時比較時間及 ID，避免同時事件跳頁。Cursor 必須屬於登入者且未移除，失效回 400，可重新整理。開啟通知中心取得最新一頁；載入較舊頁後輪詢合併最新資料並保留較舊列。登入／切換身分取消舊請求、清空通知及去重狀態；非同步結果檢查帳號及 auth generation。前景每 8 秒、背景每 30 秒更新，未登入停止；錯誤保留重試入口，不當成空清單。

## 瀏覽器與擴充

瀏覽器通知沿用個人設定及瀏覽器授權。初次載入既有事件不彈通知；頁面在背景、有授權且有新事件才顯示，點擊使用同一 typed target。瀏覽器不支援一般 Notification constructor 時，通知中心仍可用。沒有 Push subscription／service worker，關閉所有網頁後不提供系統推播。

新增事件在狀態提交處呼叫共用 publisher，沿用既有目標。新增目標同步更新後端 whitelist、前端 registry、功能深連結與授權測試，不在 publisher 寫任意 URL。結構變更提高 version 並保持舊通知可讀。未來郵件／Push 可消費同一 durable event，投遞狀態應另表保存，不改變已讀語意、不在 SQL transaction 呼叫外部服務。
