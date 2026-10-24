# 出勤打卡

結論：**卡號對應**、**刷卡匯入**（見 [刷卡匯入](attendance-imports.md)）、**假日設定**（見 [假日設定](attendance-calendar.md)）與**出勤紀錄**查詢（見 [出勤紀錄](attendance-records.md)）已完成，公文系統 GD 的連線、權限與監控見 [出勤與公文系統 GD](attendance-gd.md)；決定與取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)。頁面網址在 `/office/attendance/`，管理頁（`attendance.cards`、`attendance.imports`、`attendance.calendar`）在選單放「系統管理 → 人事與出勤」，`attendance.records` 放「公司事務」。使用者管理檢視器的「帳號」分頁列出該人的持有期間（`AttendanceUserCards` 以 `userId` 另查，Identity 不依賴出勤）：有 `attendance.cards` 才顯示，有 `.maintain` 另附以姓名搜尋的卡號對應頁連結。舊資料轉移見 [出勤舊資料轉移](../operations/attendance-migration.md)。頁首、詳情與對話框的問號開啟操作導覽。

## 授權

- `attendance.cards`：讀卡片、持有歷史、未歸戶卡號與不列入出勤名單。`attendance.cards.maintain`：新增、修改、刪除卡片、持有期間與不列入出勤，寫入端點兩者都要有。
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
- **接手**：新段開始時間落在有人持有的期間內時，沒有帶 `takeOver` 回 409 `attendance_card_held`（不帶持有人，前端由持有歷史提示是誰持有）；帶了就把前一段的 `EndsAt` 設成新段的 `StartsAt`。
- 一律拒絕：新段開始時間不晚於它重疊的既有期間的開始時間（`attendance_assignment_starts_not_after_previous`）；新段比它重疊的既有期間早結束，接手後那一截會無人持有（`attendance_assignment_overlap`）。修改既有期間時重疊一律拒絕，不接手。
- 檢查與寫入在 `AdministrativeAudit` 的 Serializable 交易與行政寫入鎖內，並發的指派會依序處理。

## 未歸戶卡號

員工卡上沒有持有期間涵蓋的刷卡（含尚未登錄的卡號），依卡號彙總筆數、第一筆與最後一筆刷卡，可依出勤日篩選。指派時起始時間預設為第一筆未歸戶刷卡。

## 刷卡匯入

見 [刷卡匯入](attendance-imports.md)。

## 不列入出勤

- HR 看全公司出勤時隱藏的人（董事、外派、兼任等），取代舊欄位「顯示打卡資料」；不是權限，本人與主管照常看得到，查詢方式見 [出勤紀錄](attendance-records.md#名單)。
- 管理在卡號對應頁的「不列入出勤」檢視：`GET /api/v1/attendance-roster-exclusions` 要 `attendance.cards`；`PUT`／`DELETE /api/v1/attendance-roster-exclusions/{userId}`（設定或修改原因、恢復列入）另要 `.maintain`。
- 以使用者為準：只有姓名、還沒綁帳號的持有人要先綁帳號才能加入。原因是選填的自由文字（最多 200 字），不進稽核。

## 出勤判定

- 規則在 `Attendance:Workday`，`FullDay`、`MorningOff`、`AfternoonOff` 各一份（預設值與驗證見 [AttendanceOptions](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceOptions.cs)）。
- 判定是 [AttendanceEvaluator](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceEvaluator.cs) 的純函式，遲到與早退以秒計；行事曆缺日時週末當放假、平日無法判定，假單無法取得時沒刷卡的日子無法判定。查詢與今天、GD 連不上的處理見 [出勤紀錄](attendance-records.md)。

## 稽核

`admin.attendance_card`、`admin.attendance_card_delete`、`admin.attendance_assignment`、`admin.attendance_assignment_delete`、`admin.attendance_roster_exclusion`、`admin.attendance_roster_exclusion_delete`，活動稽核以「出勤卡號與名單異動」篩選。持有期間的快照是該卡的全部期間（看得出接手改了前一段）；被拒絕的嘗試也會留下錯誤代碼。備註是自由文字，不進稽核（`AuditRedactor` 白名單不含）。
