# 出勤打卡

結論：**卡號對應**、**刷卡匯入**（見 [刷卡匯入](attendance-imports.md)）、**假日設定**（見 [假日設定](attendance-calendar.md)）與**出勤紀錄**查詢（見 [出勤紀錄](attendance-records.md)）已完成，公文系統 GD 的連線、權限與監控見 [出勤與公文系統 GD](attendance-gd.md)；決定與取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)。頁面網址在 `/office/attendance/`，管理頁（`attendance.cards`、`attendance.imports`、`attendance.calendar`）在選單放「系統管理 → 人事與出勤」，`attendance.records` 放「公司事務」。使用者管理檢視器的「帳號」分頁列出該人的持有期間（`AttendanceUserCards` 以 `userId` 另查，Identity 不依賴出勤）：有 `attendance.cards` 才顯示，有 `.maintain` 另附以姓名搜尋的卡號對應頁連結。舊資料轉移見 [出勤舊資料轉移](../operations/attendance-migration.md)。頁首、詳情與對話框的問號開啟操作導覽。

## 授權

- `attendance.cards`：讀卡號清單、持有歷史與每張卡的刷卡紀錄。`attendance.cards.maintain`：新增、修改、刪除卡片與持有期間，寫入端點兩者都要有。
- `attendance.imports`：讀匯入紀錄。`attendance.imports.reimport`：手動重匯，端點兩者都要有。
- `attendance.records` 以外的出勤功能（卡號對應、匯入紀錄、假日設定與其動作）列在 `AdministrativeFeatures`：管理員以測試身分扮成 HR 時可讀，寫入回 403 `test_admin_read_only`，免得紀錄把被扮演的人當成操作者。
- `attendance.records`：資料範圍 `own`、`unit`、`unit-tree`、`all`，組織勾選的主管自動取得所管單位含下層，見 [出勤紀錄](attendance-records.md#資料範圍)。
- Seed：`workspace` 群組有 `attendance.records` 的 `own`；`hr`（人事）群組有出勤的全部頁面與動作，以及 `all` 範圍的 `attendance.records`，不含使用者管理。`hr` 沒有成員也沒有角色，由管理員連結角色與加人。

## 卡片

- 一張卡號清單：打卡檔出現過的卡號匯入時自動建立（與刷卡同一個交易），HR 只在先發卡、還沒刷過時手動新增。卡號去掉前後空白後存入，不補零；唯一（不分大小寫），重複回 `attendance_card_number_duplicate`。
- 卡號建立後不改（刷卡以卡號字串對應）；可改的只有備註與「在出勤紀錄隱藏」（`HiddenFromRecords`）。
- 狀態篩選（`status`）：`held` 持有中；`unheld` 無人持有＝現在沒有持有期間涵蓋、且沒有隱藏；`hidden` 隱藏。無人持有看「現在」而不是「有沒有待指派的刷卡」：HR 看到卡無人持有就會來這裡指派，刷卡全在舊期間內的卡也要找得到。不論狀態，清單一律依卡號排序（HR 拿著卡號來找）。
- 待指派（有沒有持有期間涵蓋的刷卡，隱藏的卡除外）不另設篩選：每張卡帶最後刷卡時間與待指派的筆數、第一筆與最後一筆，列上標筆數；詳情的「補指派」以第一筆為開始、下一段期間的開始為結束。
- 詳情的刷卡紀錄（`GET /attendance-cards/{id}/punches`）從卡片的角度列出原始刷卡與當時歸給誰（沒有持有人的標「無持有人」），新到舊、每頁 50 筆、以上一頁最後一筆為游標；不判定遲到早退，那是出勤紀錄以人為主的事。
- 只有沒刷過卡、也沒有持有期間的卡可以刪除（`attendance_card_in_use`）：持有期間是歸戶依據，不級聯；有刷卡的卡刪了下次匯入又會建回來。不再使用的卡把持有期間結束，不想看到的改為隱藏。

## 在出勤紀錄隱藏

- HR 看全公司出勤時略過的卡（訪客卡、公務卡、董事的卡等），取代舊欄位「顯示打卡資料」與先前的卡片種類（員工／訪客）。不是權限：本人與主管照常看得到，指定人或卡號時也查得到，見 [出勤紀錄](attendance-records.md#名單)。
- 設在卡片上而不是人：同名同姓或只有姓名的持有人不會被一起隱藏；隱藏的卡也能指派持有人，所以換手後不必重設。

## 持有期間

- 期間是 `[StartsAt, EndsAt)`，`EndsAt` 為空表示持有中。持有人是使用者或姓名，至少一個；只有姓名的期間之後用修改綁上使用者，先前的刷卡隨之歸戶。
- 同一張卡的期間不可重疊，同一人可同時持有多張。
- **接手**：新段開始時間落在有人持有的期間內時，沒有帶 `takeOver` 回 409 `attendance_card_held`（不帶持有人，前端由持有歷史提示是誰持有）；帶了就把前一段的 `EndsAt` 設成新段的 `StartsAt`。
- 一律拒絕：新段開始時間不晚於它重疊的既有期間的開始時間（`attendance_assignment_starts_not_after_previous`）；新段比它重疊的既有期間早結束，接手後那一截會無人持有（`attendance_assignment_overlap`）。修改既有期間時重疊一律拒絕，不接手。
- 檢查與寫入在 `AdministrativeAudit` 的 Serializable 交易與行政寫入鎖內，並發的指派會依序處理。

## 刷卡匯入

見 [刷卡匯入](attendance-imports.md)。

## 出勤判定

- 規則在 `Attendance:Workday`，`FullDay`、`MorningOff`、`AfternoonOff` 各一份（預設值與驗證見 [AttendanceOptions](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceOptions.cs)）。
- 判定是 [AttendanceEvaluator](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceEvaluator.cs) 的純函式；遲到早退的精度是 `PunchPrecision`：`Second` 以秒計，`Minute` 捨去秒數（同舊系統）。行事曆缺日時週末當放假、平日無法判定，表單無法取得時沒刷卡的日子無法判定。查詢與今天、GD 連不上的處理見 [出勤紀錄](attendance-records.md)。

### 表單補足

遲到、早退與異常照舊系統判，不因表單改變；另標一個補足狀態（`GapCoverage`）。規則只寫在 `AttendanceEvaluator` 的 `Shift.Need`（這天要多少秒的表單）與 `Shift.ClockOutWith`（扣表單後的應下班），改規則改這兩處：

- **只有核准（`Decide`）的假單與出差單算數**。流程中（`Send`）的單照樣列出，並寫出停在誰，但不算補足；`Finish` 是承辦人把**退回**的單結案存查（`FLOW_BACKUP_*` 看得出先退回），不是核准，與暫存、退回、銷單一樣不讀。
- **比時數，不比時段**：核准表單的時數從當天的淨工時（上班日 8 小時）扣掉，剩下的不多於實際上班時數就補足；遲到時表單至少要抵掉彈性終點後的遲到，晚走不抵遲到（同舊系統遲到照表定下班）。表單不必對齊缺口。
- 時數都扣午休（`BreakStart`～`BreakEnd`）；表單只算彈性起點到最晚應下班之間，重疊只算一次，最多一天。設定驗證要求午休在彈性終點之後、最早應下班之前，所以沒有表單時與舊系統的遲到早退同時有、同時沒有缺口。
- 例：09:01:19 到、請 08:30～09:00，等於 08:31:19 到，扣表單後 18:01:19 可下班，17:30:22 走差 30 分 57 秒；09:10 到、18:00 走、沒請假，遲到 10 分，但只上了 7 小時 20 分，要 40 分的表單。
- 沒有刷卡：核准的表單抵掉整天就是請假，否則是異常。只有一筆刷卡仍是異常，上班時數當成 0。
- 狀態：核准的夠→已補足（`Covered`）；加上流程中的才夠→流程中（`Pending`）；都不夠→未補足（`Unexplained`），`ShortSeconds` 是核准表單還差的秒數；表單無法取得→`Unknown`。沒有缺口與今天沒有狀態。`AdjustedClockOut` 是扣核准表單後的應下班，表單不夠抵遲到或已抵掉整天時為 null。`TheFormRuleIsConsistentForEveryPunchTime` 以整天的上下班時間檢查這些性質。`Gaps` 是缺口時段（不扣表單）。

## 稽核

`admin.attendance_card`（快照含前後的隱藏旗標）、`admin.attendance_card_delete`、`admin.attendance_assignment`、`admin.attendance_assignment_delete`，活動稽核以「出勤卡號與名單異動」篩選。持有期間的快照是該卡的全部期間（看得出接手改了前一段）；被拒絕的嘗試也會留下錯誤代碼。備註是自由文字，不進稽核（`AuditRedactor` 白名單不含）。
