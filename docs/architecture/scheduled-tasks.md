# 排程任務

結論：定期要做的事（清理、同步、匯入、檢查）寫成一個實作 [`IScheduledTask`](../../backend/src/AiNexus.Platform/Scheduling/IScheduledTask.cs) 的類別，只寫業務邏輯；迴圈、跨程序租約、逾時、停止、日誌、執行紀錄與失敗通知由排程引擎（`Admin/Scheduling`）處理，管理頁「背景作業」（`/admin/scheduling`）自動列出。不要再為定期工作寫 `BackgroundService`。取捨見 [ADR 0035](../decisions/0035-scheduled-tasks.md)。

## 寫一個任務

1. 在模組的能力資料夾新增 `<名詞>Task.cs`，實作 `IScheduledTask`；組合產生器會註冊（scoped，每輪一個 DI 範圍），不必在 `AddServices` 寫。
2. `Definition`：`Key`（`<module>.<動作>`，小寫 kebab，寫進資料庫，改名＝新任務）、名稱、分類（管理頁分組，用模組的中文名）、一句說明。
   - 選填：`NotifyAfterFailures`（預設連續 3 次才通知）、`Unavailable`（功能沒開時回傳原因；只讀設定，每次排程前都會呼叫）、`FixedSchedule`（排程由任務性質決定時，目前只有 `Schedule.AtStartup(重試間隔)`：每次啟動跑一次，失敗才重試）。
3. 在 `appsettings.json` 的 `Scheduling:Tasks` 加一列 `{ "Enabled", "Schedule", "Timeout" }`。排程與逾時只寫在設定，程式沒有預設值；漏列或缺欄位時啟動失敗。
4. `RunAsync` 回傳 [`TaskOutcome`](../../backend/src/AiNexus.Platform/Scheduling/TaskOutcome.cs)：`Done(摘要)`、`Idle()`（沒事可做，紀錄只留 2 天）、`Count(筆數, 摘要)`、`Failed(錯誤)`。摘要是給管理員看的一句話，不放個人資料。
5. 長工作在迴圈裡呼叫 `context.Report(階段, 完成, 總數)`；只寫記憶體，續租時一併存檔，呼叫幾次都不會多打資料庫。
6. 範例：[`NotificationExpiryTask`](../../backend/src/AiNexus.Features/Platform/Notifications/NotificationExpiryTask.cs)（最小）、[`CalendarSyncTask`](../../backend/src/AiNexus.Features/Office/CalendarSync/Sync/CalendarSyncTask.cs)（手動參數、失敗對應）。

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

- `Scheduling:Execute`：這個程序是否執行排程，預設 `false`，只有正式站在 `appsettings.Production.json` 設 `true`。開發機與正式站共用資料庫，開發機執行等於用不同版本的程式跑同一批任務。
- `Scheduling:ExecuteOnly`：非空時只執行列出的任務 Key（仍要 `Execute=true`）。開發機要試某個任務時用它，其他任務留給正式站。
- `Scheduling:Tasks:<Key>`：每個任務的唯一設定來源，`appsettings.json` 列出全部，環境設定檔只寫要改的欄位。`Schedule` 寫間隔（`00:05:00`，上一輪結束後隔多久，新任務立即跑一次）或五欄 cron（`30 3 * * *`，台灣時間），同一個鍵，覆寫時整個取代；`FixedSchedule` 的任務不能填。缺任務、缺欄位、鍵不是已註冊的任務都會在啟動時一次列出、拒絕啟動。
- 每天一次的清理排在凌晨並互相錯開；要讓人第一時間看到的檢查排在上班時間（結構異動檢查 08:00）。
- 日誌與稽核在資料庫的保存期清理是任務（`diagnostics.retention`、`audit.retention`）；本機日誌檔由每個程序自己清，見下一節。

## 常駐服務

- 只有兩種工作常駐、不寫成任務：每個程序都得自己做的（本機日誌檔、記憶體取樣、自己手上的租約），以及要立刻反應的佇列。目前是 `DiagnosticWorker`、`ScheduledTaskHost`、`BackgroundJobWorker`、`MailDispatcher`、`GenerationWorker`、`RunRecoveryWorker`、`RuntimeSampler`。
- 寫法：繼承 `BackgroundService` 並實作 [`IResidentService`](../../backend/src/AiNexus.Platform/Scheduling/IResidentService.cs)，宣告 `Definition`（Key、名稱、分類、一句說明、節奏；選填 `Inactive`，回傳依設定不做事的原因）。組合產生器會註冊；Platform 自己的服務手動呼叫 `AddResidentService<T>()`。
- 管理頁的「常駐服務」檢視自動列出。狀態由 `ResidentServices` 直接讀 `ExecuteTask`，實作不必回報；它只在各程序的記憶體，頁面顯示的是回應請求的站台。
- 每個 hosted service 都必須宣告（`ResidentServiceTests`），不留管理頁看不到的背景動作。

## 測試

- 直接呼叫任務的公開方法（例：`NotificationExpiryTask.ExpireAsync`），在 `AppFactory` 的範圍內取得任務；不要啟動引擎等排程。
- 引擎本身的行為（認領、續租、停止、中斷、補跑、失敗通知）在 `Scheduling` 的整合測試。

## 觀察

- 管理頁：24 小時時間軸（實際輪次與預定）、執行中進度、完整執行紀錄；功能 ID 見 [`FeatureCatalog`](../../backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs) 的 `scheduling`、`scheduling.manage`。
- 監控來源 `scheduled-tasks`（[監控子系統](monitoring-sources.md)）；連續失敗達門檻通知能看「背景作業」頁的人，恢復時撤下。
- 日誌 3800–3805（[日誌事件](log-events.md)）。
