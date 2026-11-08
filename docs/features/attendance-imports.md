# 刷卡匯入

結論：打卡機每天輸出的檔由排程任務 `attendance.punch-import`（[背景作業](scheduling.md)，每 5 分鐘）定期掃描匯入 `attendance.Punches`，一個檔一個交易、任何一行錯誤整檔失敗；匯入紀錄頁（`attendance.imports`）可查詢與手動重匯。卡號歸戶與授權見 [出勤打卡](attendance.md)，取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)。

- **來源**：HAMS 轉出的 `yyyyMMdd.txt`（正式機是同機的 `C:\Export\`；開發機預設讀每晚複製過去的網路共用），其他檔名一律不匯入。轉出的時機見 [打卡機主機排程](../operations/attendance-hams.md)。IIS 應用程式集區的身分必須能讀取該資料夾（網路共用要授權給集區身分或電腦帳號）。
- **設定**：`Attendance:Import`，預設值與說明見 [AttendanceOptions](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceOptions.cs)。重點是 `SourceDirectory`、`Enabled`（只管定期掃描，手動重匯不受影響）；掃描間隔改在 `Scheduling:Tasks:attendance.punch-import`。開發機讀不到共用時在 `.local/config/appsettings.Local.json` 設 `Enabled` 為 `false`。外部設定不會自動重新載入，改完要回收應用程式集區。
- **換來源資料夾**：檔案列以檔名記錄已匯入的版本（修改時間與大小），不記資料夾；複製時保留修改時間，所以從共用改指 `C:\Export\` 只會重匯版本不同的檔。排程只新增，就算重匯也不會少刷卡。
- **掃描**：新部署立即掃一次，之後依間隔；背景作業頁可立即執行。修改時間或大小與上次成功匯入的不同、從沒匯入過、被標記重匯，或最近一次失敗就匯入；最近一次失敗而檔案沒變，要等 `FailedRetrySeconds`（預設 900）。修改時間距今不到 `SettleSeconds`（預設 30）的檔先等，避免讀到寫到一半的檔。
- **解析**：每行 `機器代號:卡號:yyyyMMdd:HHmmss:事件代號`，第一欄與最後一欄原樣保存（實際是 HAMS 轉出範本的固定字 `001`、`11`，見 ADR 0018）。打卡機實際把整行包在雙引號裡，整行或各欄外圍成對的雙引號先去掉，剩下的雙引號視為格式錯誤：留著會存成 `"001`，與舊系統轉入的 `001` 被當成兩筆刷卡。刷卡時間以 +08:00 存放，出勤日依刷卡時間而非檔名。UTF-8（可有 BOM）或 UTF-16；空行與 Ctrl-Z 檔尾略過。**任何一行格式錯誤整個檔案匯入失敗**，不略過：匯入紀錄沒有略過筆數可記，靜默少一筆刷卡會變成假缺勤。
- **排程只新增**：HAMS 的檔只附加（見 [打卡機主機排程](../operations/attendance-hams.md)），每次重讀整檔、只寫資料庫還沒有的刷卡；檔案少了行或被清空也不刪。同一張卡一天可以有好幾行，判定只取最早與最晚。
- **去重**：（卡號、時間、機器代號）不分檔案，已在資料庫的刷卡歸先寫入的檔；只載入新內容涵蓋的出勤日比對。`PunchCount` 是該檔實際擁有的筆數。
- **手動重匯整檔取代**：該檔先前寫入、新內容已沒有的刷卡一併刪除（只動有差異的列），HR 修正檔案後重匯即可，不必寫 SQL。被刪的刷卡可能也在別的檔（例如在 HAMS 畫面手動轉出過）：同一交易把檔名日期不早於它出勤日、已匯入的檔標記為要重匯（清空 `ImportedModifiedAt`、`ImportedSize`），下一次掃描以只新增補回。
- **排程的稽核**：寫入了新刷卡的排程匯入，在同一交易另記系統稽核 `attendance.punch_file.synced`（擁有者為空，明細是新刷卡數與自動建立的卡片數）；沒有新刷卡的掃描不記。
- **失敗**：交易整個回復，`Punches` 與檔案的已匯入版本維持上次成功；`PunchImports` 的 failed 紀錄在回復之後另一個交易寫入，並更新 `LastImportId`。連失敗紀錄都寫不進去（資料庫不可用）時沒有紀錄，下一輪再試。
- **並行**：讀檔與解析不持鎖，寫入前取匯入鎖（`AttendanceLocks.PunchImport`），本機與 IIS 同時跑也一次只寫入一個檔；排程等到鎖後會再判斷一次，已匯入的版本就跳過。手動重匯的檔之間排程可插入。`Punches` 的唯一索引是最後防線。
- **手動重匯**：`POST /api/v1/attendance-imports`，body `{ "from": "2026-10-01", "to": "2026-10-22" }`：檔名日期、含頭尾（起訖相同即單一檔），不晚於台灣今天、最多 31 天。請求內同步、由舊到新逐檔重匯，不論有沒有變都整檔取代，記為 `manual` 並帶操作者，稽核 `attendance.punch_file.imported` 記下區間、重匯與找不到的檔數。回 200：各檔的匯入紀錄（含 failed）與找不到的檔名。整段都沒有檔 `attendance_import_file_not_found`（404）、資料夾讀不到 `attendance_import_source_unavailable`（503），不留匯入紀錄（已完成的檔保留）。
- **查詢**：`GET /api/v1/attendance-imports`，新到舊，排序、欄位篩選與分頁走共用表格查詢（[ADR 0026](../decisions/0026-shared-table-queries.md)）；可依檔案（`fileId`、`fileName`）、狀態與開始時間期間篩選，帶出檔名與操作者姓名。
- **匯入明細**：點一筆開詳情。`GET /api/v1/attendance-imports/{id}/punches` 列出這次寫入的刷卡（卡號、時間、持有人、代號），與 `PunchCount` 一致，不含歸給別的檔的重複刷卡。刷卡依檔案保存、不依匯入保存，所以只有該檔最近一次成功的匯入（`isCurrent`）列得出來；失敗或已被之後的匯入取代的回 `attendance_import_not_current`（409），畫面改列結果與來源資訊。持有人是讀取當下的歸戶結果。
- **監控**：子系統 `attendance-import`，判定見 [監控子系統](../architecture/monitoring-sources.md)。沒有新刷卡時 HAMS 不寫檔，所以過期看「上次成功之後有沒有整天過去的工作日」（GD 行事曆，讀不到時以週一到週五計），週末與連假不會誤報。資料夾讀不到記為排程任務的失敗（`attendance_punch_source_unavailable`），監控讀最近一輪；連續失敗由排程通知。
