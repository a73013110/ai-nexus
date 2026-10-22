# 出勤打卡

結論：目前完成**卡號對應**、**刷卡匯入**、**假日設定**（見 [假日設定](attendance-calendar.md)）與**出勤紀錄**查詢（見 [出勤紀錄](attendance-records.md)）的後端，公文系統 GD 的連線、權限與監控見 [出勤與公文系統 GD](attendance-gd.md)；決定與取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)。頁面網址在 `/staff/attendance/`，管理頁（`attendance.cards`、`attendance.imports`、`attendance.calendar`）在選單放「系統管理 → 人事與出勤」，`attendance.records` 放「員工服務」。四個頁面目前是前端佔位頁（「建置中」）。

## 授權

- `attendance.cards`：讀卡片、持有歷史、未歸戶卡號。`attendance.cards.maintain`：新增、修改、刪除卡片與持有期間，寫入端點兩者都要有。
- `attendance.imports`：讀匯入紀錄。`attendance.imports.reimport`：手動重匯，端點兩者都要有。
- `attendance.records` 以外的出勤功能（卡號對應、匯入紀錄、假日設定與其動作）列在 `AdministrativeFeatures`：管理員以測試身分扮成 HR 時可讀，寫入回 403 `test_admin_read_only`，免得紀錄把被扮演的人當成操作者。
- `attendance.records`：資料範圍 `own`、`unit`、`unit-tree`、`all`，組織勾選的主管自動取得所管單位含下層，見 [出勤紀錄](attendance-records.md#資料範圍)。
- Seed：`workspace` 群組有 `attendance.records` 的 `own`；`hr`（人事）群組有出勤的全部頁面與動作，以及 `all` 範圍的 `attendance.records`，不含使用者管理。`hr` 沒有成員也沒有角色，由管理員連結角色與加人。

## 卡片

- 卡號去掉前後空白後存入，不補零；唯一（不分大小寫），重複回 `attendance_card_number_duplicate`。
- 員工卡才歸戶。訪客卡不能指派持有期間（`attendance_card_visitor`），也不能把已有持有期間的卡改成訪客卡。
- 只有沒有任何持有期間的卡可以刪除（`attendance_card_has_assignments`）：持有期間是歸戶依據，不級聯；不再使用的卡把持有期間結束即可。

## 持有期間

- 期間是 `[StartsAt, EndsAt)`，`EndsAt` 為空表示持有中。持有人是使用者或姓名，至少一個；只有姓名的期間之後用修改綁上使用者，先前的刷卡隨之歸戶。
- 同一張卡的期間不可重疊，同一人可同時持有多張。
- **接手**：新段開始時間落在有人持有的期間內時，沒有帶 `takeOver` 回 409 `attendance_card_held`（一般錯誤回應，不帶持有人；前端由持有歷史提示目前是誰持有）；帶了就把前一段的 `EndsAt` 設成新段的 `StartsAt`。
- 一律拒絕：新段開始時間不晚於它重疊的既有期間的開始時間（`attendance_assignment_starts_not_after_previous`）；新段比它重疊的既有期間早結束，接手後那一截會無人持有（`attendance_assignment_overlap`）。修改既有期間時重疊一律拒絕，不接手。
- 檢查與寫入在 `AdministrativeAudit` 的 Serializable 交易與行政寫入鎖內，並發的指派會依序處理。

## 未歸戶卡號

員工卡上沒有持有期間涵蓋的刷卡（含尚未登錄的卡號），依卡號彙總筆數、第一筆與最後一筆刷卡，可依出勤日篩選。第一筆未歸戶刷卡的時間是指派時起始時間的預設值。

## 刷卡匯入

- **來源**：打卡機每天輸出 `yyyyMMdd.txt` 到共用資料夾；其他檔名一律不匯入。IIS 應用程式集區的身分必須能讀取該資料夾（網路共用要授權給集區身分或電腦帳號）。
- **設定**：`Attendance:Import`，預設值與說明見 [AttendanceOptions](../../backend/src/AiNexus.Features/Staff/Attendance/AttendanceOptions.cs)。重點是 `SourceDirectory`、`ScanIntervalSeconds`（預設 300）、`Enabled`（只管背景掃描，手動重匯不受影響）。開發機讀不到共用時在 `.local/config/appsettings.Local.json` 設 `Enabled` 為 `false`。
- **掃描**：`PunchImportWorker` 啟動即掃一次，之後依間隔。修改時間或大小與上次成功匯入的不同、從沒匯入過、被標記重匯，或最近一次失敗就匯入；最近一次失敗而檔案沒變，要等 `FailedRetrySeconds`（預設 900）。修改時間距今不到 `SettleSeconds`（預設 30）的檔先等，避免讀到寫到一半的檔。
- **解析**：每行 `機器代號:卡號:yyyyMMdd:HHmmss:事件代號`，第一欄與最後一欄原樣保存。刷卡時間以 +08:00 存放，出勤日依刷卡時間而非檔名。UTF-8（可有 BOM）或 UTF-16；空行與 Ctrl-Z 檔尾略過。**任何一行格式錯誤整個檔案匯入失敗**，不略過：匯入紀錄沒有略過筆數可記，靜默少一筆刷卡會變成假缺勤。錯誤訊息列出前三個行號與原因，不含檔案內容。
- **快照重匯**：一個交易內把該檔列成新內容（只動有差異的列）。新內容裡已屬於別的檔的刷卡不重寫，歸先寫入的檔；`PunchCount` 是該檔實際擁有的筆數。
- **鄰檔補救**：同一筆刷卡出現在相鄰兩天的檔時屬於先匯入的檔。該檔重匯而新內容沒有這一筆，刪除會讓資料消失，所以刪除刷卡時同一交易內把出勤日前一天、當天與後一天的檔標記為要重匯（清空 `ImportedModifiedAt`、`ImportedSize`），排程同一次掃描最多三輪內補回。代價是多一次內容不變的匯入。
- **失敗**：交易整個回復，`Punches` 與檔案的已匯入版本維持上次成功；`PunchImports` 的 failed 紀錄在回復之後另一個交易寫入，並更新 `LastImportId`。連失敗紀錄都寫不進去（資料庫不可用）時沒有紀錄，下一輪再試。
- **並行**：`PunchImportGate` 讓排程與手動重匯一次只跑一個（程序內；跨檔去重與鄰檔補救會動別的檔的列，所以不是每檔一把鎖）。多程序重疊時由 `Punches` 的唯一索引擋下，後到的記為失敗並重試。
- **手動重匯**：`POST /api/v1/attendance-imports`，body `{ "date": "2026-10-01" }`（檔名日期）。不論檔案有沒有變都重匯，`TriggerKind` 為 `manual`、`RequestedById` 為操作者。找不到檔 `attendance_import_file_not_found`（404）、資料夾讀不到 `attendance_import_source_unavailable`（503），都不留紀錄；檔案存在但匯入失敗回 200 與 failed 的紀錄。不另寫稽核，匯入紀錄就是紀錄。
- **查詢**：`GET /api/v1/attendance-imports`，新到舊、游標分頁；可依檔案（`fileId`、`fileName`）、狀態與開始時間期間篩選，帶出檔名與操作者姓名。
- **監控**：子系統 `attendance-import`，判定見 [監控子系統](../architecture/monitoring-sources.md)。日誌事件 3400–3406；來源資料夾讀不到與恢復只在狀態轉換時各記一次。

## 出勤判定

- 規則在 `Attendance:Workday`，`FullDay`、`MorningOff`、`AfternoonOff` 各一份（預設值與驗證見 [AttendanceOptions](../../backend/src/AiNexus.Features/Staff/Attendance/AttendanceOptions.cs)）。
- 判定是 [AttendanceEvaluator](../../backend/src/AiNexus.Features/Staff/Attendance/Records/AttendanceEvaluator.cs) 的純函式，遲到與早退以秒計；行事曆缺日時週末當放假、平日無法判定，假單無法取得時沒刷卡的日子無法判定。查詢與今天、GD 連不上的處理見 [出勤紀錄](attendance-records.md)。

## 稽核

`admin.attendance_card`、`admin.attendance_card_delete`、`admin.attendance_assignment`、`admin.attendance_assignment_delete`，活動稽核以「出勤卡號異動」篩選。持有期間的快照是該卡的全部期間（看得出接手改了前一段）；被拒絕的嘗試也會留下錯誤代碼。備註是自由文字，不進稽核（`AuditRedactor` 白名單不含）。
