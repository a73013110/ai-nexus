# Google 日曆同步

結論：排程任務 `calendar-sync.sync`（[背景作業](scheduling.md)）定期把公文系統（GD）的假單與出差單同步成 Google 共用日曆上的事件，取代 ADAuthentication 的 `EDocSyncToGoogleCalendar`。管理頁（`/office/calendar-sync`）看狀態、事件與執行紀錄，可立即同步與測試連線。取捨見 [ADR 0034](../decisions/0034-calendar-sync.md)。

## 同步什麼

- **來源**：`Form_Leave`（跨區間假單取 `Form_Leave_Group` 的各段）與 `Form_Travel`，流程 `Send`（簽核中）與 `Decide`（已核准）。`Finish` 是退回結案，不同步（舊系統會）。讀取的表與授權同 [出勤的 GD 帳號](attendance-gd.md)。
- **期間窗**：結束時間在最近 `LookbackDays`（預設 30）天內或之後的段。窗外的舊事件不動，也不因 GD 沒有而刪除。
- **內容**：標題「【請假】姓名」「【出差】姓名」；說明列代理人、狀態、文號；請假顏色 1、出差 4（與舊系統相同）。請假的人取 `LeaveUserId`（舊系統用填單人）。
- **比對**：每段在 `calendarsync.CalendarEvents` 一列，鍵是（日曆 ID、文號、段）。內容雜湊不同就更新；GD 沒有了（撤回、銷單、刪除）就從日曆刪除；GD 又出現就救回。

## 怎麼保證不重複、不遺漏

- 事件 ID 由列 ID 決定，建立遇 409 就接手同一個事件；更新遇 404／410（有人手動刪掉）就重建；刪除遇 404 算完成。重推幾次結果都一樣。
- 推送失敗記在列上（原因代碼、訊息），依 1、2、4… 分鐘退避到 `MaxRetryDelayMinutes`；Google 給 `Retry-After` 時取較長者。憑證、權限或日曆找不到時整輪停下，不讓每列各記一次。
- GD 連不上時這一輪不比對、不刪除任何事件。
- **安全閥**：要移除的數量 ≥ `RemovalHoldMinimum` 且超過窗內事件的 `RemovalHoldRatio` 時暫停移除，管理頁按「確認移除」才執行。新增與更新照常。
- 開發與正式共用資料庫：列以日曆 ID 區隔，同時只有一輪由排程租約保證（所有日曆共用 `calendar-sync.sync` 這一把），執行的站台由 `Scheduling:Execute` 決定。`Enabled` 預設 `false`，只在正式站打開。

## 設定

區段 `Google`（[GoogleOptions](../../backend/src/AiNexus.Features/Platform/Google/GoogleOptions.cs)）與 `CalendarSync`（[CalendarSyncOptions](../../backend/src/AiNexus.Features/Office/CalendarSync/CalendarSyncOptions.cs)），預設值在 `appsettings.json`。

1. 在 Google Cloud 建服務帳戶並下載 JSON 金鑰，放在站外（例：`config/google-calendar.json`），只開放給集區身分讀取。`Google:ServiceAccountKeyFile` 指向它（相對路徑以 app 為準）。
2. 把共用日曆分享給服務帳戶的信箱，權限「變更活動」。管理頁的工具列會顯示服務帳戶信箱。
3. 設 `CalendarSync:CalendarId`（日曆設定裡的「日曆 ID」），先按「測試連線」。
4. 確認無誤後設 `CalendarSync:Enabled=true`、回收集區。啟用時讀不到金鑰會啟動失敗。

`Google:DelegatedUser` 在用 Workspace 網域委派、以某個成員身分寫入時填（舊系統就是這樣，沿用同一把金鑰與委派帳號即可）；分享日曆給服務帳戶的做法留空。網域委派要在管理控制台替該用戶端 ID 授權 `https://www.googleapis.com/auth/calendar`（舊系統已授權）；要求的範圍不在清單裡時換權杖會被拒（`unauthorized_client`），測試連線會顯示原因。委派帳號也要能編輯目標日曆（擁有者或已共用「變更活動」）。

## 開發機測試

用正式金鑰加一個測試日曆跑完整同步，正式日曆不受影響；但讀的是正式 GD，測試日曆上會出現真實同事的假單，測完要清掉。

1. `.local/config/appsettings.Local.json`：`CalendarSync` 設 `Enabled=true`、`CalendarId`＝測試日曆、`LookbackDays` 調小；`Scheduling` 設 `Execute=true`、`ExecuteOnly=["calendar-sync.sync"]`，要看得快再加 `Tasks:calendar-sync.sync:Schedule="00:01:00"`。重新啟動。
2. 依序確認：測試連線顯示測試日曆名稱；第一輪後事件數與 Google 一致，抽查文號、時間、代理人、狀態；再同步一次三個數字都是 0；推送中途停站、重啟後接著推完且不重複；暫時取消日曆共用時這一輪記為失敗、事件留在待同步，加回後立即同步就補上。
3. 要從頭再測一次：管理頁「⋯ → 重設日曆」（見下方管理頁），刪掉測試日曆上的事件與這個日曆的紀錄，下一輪重新建立。
4. 收尾：先「重設日曆」（日曆清單才不會一直列出它），再把 `CalendarSync:Enabled` 與 `Scheduling:Execute` 改回 `false`、刪掉測試日曆。

正式站上線本功能後，開發機測試會和正式站搶同一把租約：輪到開發機時正式日曆那一輪延後。只在短時間內測試。

## 管理頁

- 授權：頁面 `calendar-sync`（看），動作 `calendar-sync.manage`（立即同步、確認移除、測試連線、重設日曆）；種子授予 administrators。
- 日曆：工具列可切換資料庫裡出現過的日曆（共用資料庫裡其他站台的、舊系統轉入的），預設看本站的 `CalendarId`，「全部日曆」時表格多一欄日曆。立即同步、確認移除與測試連線只作用在本站的日曆。
- 數字：待同步與推送失敗的數量在表格標題旁（跟著所選日曆），「最近檢查」在工具列。沒有異動的排程輪只更新「最近檢查」，不寫執行紀錄；有異動、失敗、手動或剛出問題的一輪才寫，保存 `RunRetentionDays` 天。
- 事件預設列同步範圍內的（回看期間內、未移除），「全部歷史」才含舊事件與已移除的。
- 點一輪看逐筆明細（`SyncRunItems`，隨紀錄連帶刪除）：結果、變更的欄位、移除原因（查 GD 現況）、失敗原因；被安全閥擋下的移除也列出。原因跟著待推送的列走（`PendingCause`），隔輪才推成功也寫得出來。
- 立即同步只排進背景就回應，不等 Google；它不等失敗事件的退避時間，修好設定後按一次就全部補上。
- 重設日曆（「⋯」選單，要照打日曆 ID 前 8 碼）：刪除 Google 上本系統記錄的事件，全部刪完才清掉這個日曆的 `CalendarEvents` 與 `SyncRuns`；手動在 Google 建立的事件不動。交給同步任務執行，與排程中的同步不會同時跑；被 Google 限速時暫停 `RateLimitPauseSeconds` 秒（`Retry-After` 更久時聽它的）後繼續，刪不掉時保留列並顯示原因，再按一次接著刪。下一輪同步會依 GD 重新建立全部事件，要讓日曆保持空的先關掉 `Enabled`。
- 查詢事件與紀錄寫稽核（請假是個人資料）；一輪有改到日曆時另寫系統稽核 `calendar-sync.run.synced`，重設寫 `calendar-sync.reset.requested`（誰要求）與 `calendar-sync.calendar.cleared`（刪了多少）。

## 監控與日誌

- 即時監控 `calendar-sync`：條件見 [監控來源](../architecture/monitoring-sources.md)。
- 日誌 3700～3704（見 [日誌事件](../architecture/log-events.md)），只在狀態轉換時記，中斷期間不每輪洗版。

## 從舊系統轉入

一次性 SQL 把 `EDocSyncToGoogleCalendar` 的列轉進 `calendarsync.CalendarEvents`：保留 Google 事件 ID 與 `UUID`（`LegacyUuid`），標為已同步、雜湊歸零；舊表的 `DayOfWeek` 已是「日期＋星期」（例：`20250402星期三`），直接當段鍵。第一輪會因雜湊不同把每個事件更新一次（套用新的內容格式），不會重複建立；舊系統沒寫回事件 ID 的列先以 UUID 找回事件再接手。轉入後停用舊系統的排程。
