# 刷卡匯入

結論：打卡機每天輸出的檔由 `PunchImportWorker` 定期掃描匯入 `attendance.Punches`，一個檔一個交易、任何一行錯誤整檔失敗；匯入紀錄頁（`attendance.imports`）可查詢與手動重匯。卡號歸戶與授權見 [出勤打卡](attendance.md)，取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)。

- **來源**：打卡機每天輸出 `yyyyMMdd.txt` 到共用資料夾；其他檔名一律不匯入。IIS 應用程式集區的身分必須能讀取該資料夾（網路共用要授權給集區身分或電腦帳號）。
- **設定**：`Attendance:Import`，預設值與說明見 [AttendanceOptions](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceOptions.cs)。重點是 `SourceDirectory`、`ScanIntervalSeconds`（預設 300）、`Enabled`（只管背景掃描，手動重匯不受影響）。開發機讀不到共用時在 `.local/config/appsettings.Local.json` 設 `Enabled` 為 `false`。
- **掃描**：`PunchImportWorker` 啟動即掃一次，之後依間隔。修改時間或大小與上次成功匯入的不同、從沒匯入過、被標記重匯，或最近一次失敗就匯入；最近一次失敗而檔案沒變，要等 `FailedRetrySeconds`（預設 900）。修改時間距今不到 `SettleSeconds`（預設 30）的檔先等，避免讀到寫到一半的檔。
- **解析**：每行 `機器代號:卡號:yyyyMMdd:HHmmss:事件代號`，第一欄與最後一欄原樣保存。打卡機實際把整行包在雙引號裡，整行或各欄外圍成對的雙引號先去掉，剩下的雙引號視為格式錯誤：留著會存成 `"001`，與舊系統轉入的 `001` 被當成兩筆刷卡。刷卡時間以 +08:00 存放，出勤日依刷卡時間而非檔名。UTF-8（可有 BOM）或 UTF-16；空行與 Ctrl-Z 檔尾略過。**任何一行格式錯誤整個檔案匯入失敗**，不略過：匯入紀錄沒有略過筆數可記，靜默少一筆刷卡會變成假缺勤。
- **快照重匯**：一個交易內把該檔列成新內容（只動有差異的列）。新內容裡已屬於別的檔的刷卡不重寫，歸先寫入的檔；`PunchCount` 是該檔實際擁有的筆數。
- **鄰檔補救**：同一筆刷卡出現在相鄰兩天的檔時屬於先匯入的檔。該檔重匯而新內容沒有這一筆，刪除會讓資料消失，所以刪除刷卡時同一交易內把出勤日前一天、當天與後一天的檔標記為要重匯（清空 `ImportedModifiedAt`、`ImportedSize`），排程同一次掃描最多三輪內補回。
- **失敗**：交易整個回復，`Punches` 與檔案的已匯入版本維持上次成功；`PunchImports` 的 failed 紀錄在回復之後另一個交易寫入，並更新 `LastImportId`。連失敗紀錄都寫不進去（資料庫不可用）時沒有紀錄，下一輪再試。
- **並行**：`PunchImportGate` 讓排程與手動重匯一次只匯入一個檔（程序內一把全域鎖，原因見程式註解），手動重匯的檔之間排程可插入。多程序重疊時由 `Punches` 的唯一索引擋下，後到的記為失敗並重試。
- **手動重匯**：`POST /api/v1/attendance-imports`，body `{ "from": "2026-10-01", "to": "2026-10-22" }`：檔名日期、含頭尾（起訖相同即單一檔），不晚於台灣今天、最多 31 天。請求內同步、由舊到新逐檔重匯，不論有沒有變，記為 `manual` 並帶操作者。回 200：各檔的匯入紀錄（含 failed）與找不到的檔名。整段都沒有檔 `attendance_import_file_not_found`（404）、資料夾讀不到 `attendance_import_source_unavailable`（503），不留紀錄（已完成的檔保留）。不另寫稽核。
- **查詢**：`GET /api/v1/attendance-imports`，新到舊，排序、欄位篩選與分頁走共用表格查詢（[ADR 0026](../decisions/0026-shared-table-queries.md)）；可依檔案（`fileId`、`fileName`）、狀態與開始時間期間篩選，帶出檔名與操作者姓名。
- **匯入明細**：點一筆開詳情。`GET /api/v1/attendance-imports/{id}/punches` 列出這次寫入的刷卡（卡號、時間、持有人、代號），與 `PunchCount` 一致，不含歸給鄰檔的重複刷卡。刷卡依檔案保存、不依匯入保存，所以只有該檔最近一次成功的匯入（`isCurrent`）列得出來；失敗或已被之後的匯入取代的回 `attendance_import_not_current`（409），畫面改列結果與來源資訊。持有人是讀取當下的歸戶結果。
- **監控**：子系統 `attendance-import`，判定見 [監控子系統](../architecture/monitoring-sources.md)。日誌事件 3400–3406；資料夾讀不到與恢復只在轉換時各記一次。
