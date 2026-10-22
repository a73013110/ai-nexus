# 出勤打卡

結論：目前只完成**卡號對應**的後端（卡片、持有期間、未歸戶卡號）；刷卡匯入、出勤紀錄與假日設定隨後加入，決定與取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)。頁面網址在 `/staff/attendance/`，管理頁（`attendance.cards`、`attendance.imports`、`attendance.calendar`）在選單放「系統管理 → 人事與出勤」，`attendance.records` 放「員工服務」。四個頁面目前是前端佔位頁（「建置中」）。

## 授權

- `attendance.cards`：讀卡片、持有歷史、未歸戶卡號。`attendance.cards.maintain`：新增、修改、刪除卡片與持有期間，寫入端點兩者都要有。
- `attendance.imports.reimport`、`attendance.calendar.maintain` 先在目錄宣告，端點隨匯入與假日設定加入。
- `attendance.records` 宣告了資料範圍（`own`、`unit`、`unit-tree`、`all`），**端點還沒有，範圍尚未套用**；加端點時依 [資料範圍](../architecture/data-scopes.md) 套用，並補進該文件的對照表。
- Seed：`workspace` 群組有 `attendance.records` 的 `own`；`hr`（人事）群組有出勤的全部頁面與動作，以及 `all` 範圍的 `attendance.records`，不含使用者管理。`hr` 沒有成員也沒有角色，由管理員連結角色與加人。
- 主管的 `unit-tree` 預設**尚未實作**（主管身分在組織單位的成員設定，不是角色），留到出勤紀錄步驟；目前由管理員建立群組授予。

## 卡片

- 卡號去掉前後空白後存入，不補零；唯一（不分大小寫），重複回 `attendance_card_number_duplicate`。
- 員工卡才歸戶。訪客卡不能指派持有期間（`attendance_card_visitor`），也不能把已有持有期間的卡改成訪客卡。
- 只有沒有任何持有期間的卡可以刪除（`attendance_card_has_assignments`）：持有期間是歸戶依據，不級聯；不再使用的卡把持有期間結束即可。

## 持有期間

- 期間是 `[StartsAt, EndsAt)`，`EndsAt` 為空表示持有中。持有人是使用者或姓名，至少一個；只有姓名的期間之後用修改綁上使用者，先前的刷卡隨之歸戶。
- 同一張卡的期間不可重疊，同一人可同時持有多張。
- **接手**：新段開始時間落在有人持有的期間內時，沒有帶 `takeOver` 回 409 `attendance_card_held`，body 是 `CardHeldResponse`（含目前的持有期間，前端據此提示）；帶了就把前一段的 `EndsAt` 設成新段的 `StartsAt`。
- 一律拒絕：新段開始時間不晚於它重疊的既有期間的開始時間（`attendance_assignment_starts_not_after_previous`）；新段會把既有期間切成兩段（`attendance_assignment_overlap`）。修改既有期間時重疊一律拒絕，不接手。
- 檢查與寫入在 `AdministrativeAudit` 的 Serializable 交易與行政寫入鎖內，並發的指派會依序處理。

## 未歸戶卡號

員工卡上沒有持有期間涵蓋的刷卡（含尚未登錄的卡號），依卡號彙總筆數、第一筆與最後一筆刷卡，可依出勤日篩選。第一筆未歸戶刷卡的時間是指派時起始時間的預設值。

## 稽核

`admin.attendance_card`、`admin.attendance_card_delete`、`admin.attendance_assignment`、`admin.attendance_assignment_delete`，活動稽核以「出勤卡號異動」篩選。持有期間的快照是該卡的全部期間（看得出接手改了前一段）；被拒絕的嘗試也會留下錯誤代碼。備註是自由文字，不進稽核（`AuditRedactor` 白名單不含）。
