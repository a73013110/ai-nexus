# 排程任務

結論：定期要做的事（清理、同步、匯入、檢查）寫成一個實作 [`IScheduledTask`](../../backend/src/AiNexus.Platform/Scheduling/IScheduledTask.cs) 的類別，只寫業務邏輯；迴圈、跨程序租約、逾時、停止、日誌、執行紀錄與失敗通知由排程引擎（`Admin/Scheduling`）處理，管理頁「背景作業」（`/admin/scheduling`）自動列出。不要再為定期工作寫 `BackgroundService`。取捨見 [ADR 0035](../decisions/0035-scheduled-tasks.md)。

## 寫一個任務

1. 在模組的能力資料夾新增 `<名詞>Task.cs`，實作 `IScheduledTask`；組合產生器會註冊（scoped，每輪一個 DI 範圍），不必在 `AddServices` 寫。
2. `Definition`：`Key`（`<module>.<動作>`，小寫 kebab，寫進資料庫，改名＝新任務）、名稱、分類（管理頁分組，用模組的中文名）、一句說明、`Schedule`。
   - `Schedule.Every(間隔)`：上一輪結束後隔多久；新任務立即跑一次。
   - `Schedule.Daily(時, 分)`、`Schedule.Cron(運算式, 說法)`：台灣時間。
   - `Schedule.AtStartup(重試間隔)`：每次啟動跑一次，失敗才重試。
   - 選填：`Timeout`（預設 30 分鐘）、`NotifyAfterFailures`（預設連續 3 次才通知）、`Unavailable`（功能沒開時回傳原因；只讀設定，每次排程前都會呼叫）。
3. `RunAsync` 回傳 [`TaskOutcome`](../../backend/src/AiNexus.Platform/Scheduling/TaskOutcome.cs)：`Done(摘要)`、`Idle()`（沒事可做，紀錄只留 2 天）、`Count(筆數, 摘要)`、`Failed(錯誤)`。摘要是給管理員看的一句話，不放個人資料。
4. 長工作在迴圈裡呼叫 `context.Report(階段, 完成, 總數)`；只寫記憶體，續租時一併存檔，呼叫幾次都不會多打資料庫。
5. 範例：[`NotificationExpiryTask`](../../backend/src/AiNexus.Features/Platform/Notifications/NotificationExpiryTask.cs)（最小）、[`CalendarSyncTask`](../../backend/src/AiNexus.Features/Office/CalendarSync/Sync/CalendarSyncTask.cs)（手動參數、失敗對應）。

## 必須可重複執行

- 執行可能在任何時刻被停站或「停止」中斷，下一輪（可能在另一個程序）重來：分批提交、以資料狀態判斷還要做什麼，不依賴上一輪留在記憶體的東西。
- 大量刪除或更新用 [`Batches.RepeatAsync`](../../backend/src/AiNexus.Platform/Data/Batches.cs)（每批 `Take(Batches.Size)`，自動提交、不升級成表鎖），不要自己寫迴圈。
- 監控或頁面要顯示任務的結果時讀執行紀錄（`ScheduledTasks.GetStateAsync`），不放在記憶體：任務可能在另一個程序執行。
- 預期內的失敗回傳 `Failed`，錯誤定義在 `<Module>Errors`；例外一樣記為失敗，`ExternalServiceException` 沿用它的錯誤代碼。紀錄只存審閱過的提示與查證代碼。
- 尊重 `ct`：停止、逾時與停站都經由它；被取消時直接拋出，不要吞掉。

## 引擎保證

- 同一任務同時只有一個程序在跑：條件式 `UPDATE` 認領 `scheduling.ScheduledTasks` 的租約（60 秒，每 5 秒續租），結果以 `LeaseToken` fencing 寫回；租約過期由其他程序接手並記為「中斷」。
- 錯過的時間（停站、暫停）只補跑一次；下一輪一律在上一輪結束後才排，不會重疊。
- `AtStartup` 任務只由啟動它的程序認領：IIS 重疊回收時舊程序不會搶跑新版本的啟動工作。
- 同時執行的任務上限 `Scheduling:MaxConcurrency`；慢的任務不擋其他任務。
- 手動執行在同一程序內立即喚醒；正在跑時排在這一輪結束後。暫停只停排程，仍可手動執行。

## 手動參數

要從其他模組帶參數觸發（例：日曆同步的「確認移除」），呼叫 `ScheduledTasks.RequestRunAsync(Key, 執行者, 參數物件)`，任務裡以 `context.Parameters<T>()` 讀回；只有手動觸發會帶參數。

## 設定與執行位置

- `Scheduling:Execute`：這個程序是否執行排程。開發機與正式站共用資料庫，開發機在 `appsettings.Local.json` 設 `false`，否則兩邊搶著用不同版本的程式跑同一批任務。
- `Scheduling:Tasks:<Key>`：個別覆寫 `Enabled`、`Interval` 或 `Cron`、`Timeout`；鍵必須是已註冊的任務，啟動時驗證。其他鍵見 [設定](../development/configuration.md)。
- 不是定期工作的 `BackgroundService` 仍然存在：使用者送出的佇列（`BackgroundJobWorker`、`MailDispatcher`、`GenerationWorker`）、取樣器（`RuntimeSampler`）、日誌管線（`DiagnosticWorker`，清理本機日誌檔，每個程序都要做，不能只由持有租約的那個做）。佇列工作在管理頁另一個檢視列出。

## 測試

- 直接呼叫任務的公開方法（例：`NotificationExpiryTask.ExpireAsync`），在 `AppFactory` 的範圍內取得任務；不要啟動引擎等排程。
- 引擎本身的行為（認領、續租、停止、中斷、補跑、失敗通知）在 `Scheduling` 的整合測試。

## 觀察

- 管理頁：倒數、執行中進度、最近 24 輪、完整執行紀錄；功能 ID 見 [`FeatureCatalog`](../../backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs) 的 `scheduling`、`scheduling.manage`。
- 監控來源 `scheduled-tasks`（[監控子系統](monitoring-sources.md)）；連續失敗達門檻通知能看「背景作業」頁的人，恢復時撤下。
- 日誌 3800–3805（[日誌事件](log-events.md)）。
