# 出勤紀錄

結論：`GET /api/v1/attendance-records?from=2026-10-01&to=2026-10-31`（頁面 `attendance.records`）回傳資料範圍內每人每天的判定結果、上下班刷卡、應下班、遲到與早退秒數，以及當天的假單與出差單；同一組條件可[匯出 Excel](#匯出-excel)，頁首「判定規則」面板顯示[規則說明](#規則說明)。判定規則見 [出勤打卡](attendance.md#出勤判定)，取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)；回應形狀是 `contracts/openapi.json` 的 `AttendanceRecordsDto`。頁首的問號可開啟操作導覽。

## 查詢

- 起訖是出勤日，都含、必填，最長 366 天（整年含閏年；一次匯出整年或看一個人的整年）；缺一端、顛倒或超過回 400 `attendance_records_range_invalid`。
- 依人分頁：`offset`、`limit`（預設 20、最多 50），依姓名排序，`total` 是符合條件的人數。區間放寬到一年後，一頁的列數是人數 × 天數，不分頁會過大；每人的天數不切開。
- 篩選：`userId`、`card`（沒有帳號的資料，見[名單](#名單)）；`unitId` 是該單位及下層的目前成員。都與資料範圍取交集，範圍外的人只是查不到，不回錯誤。格式不對（卡號過長、分頁超出）回 400 `attendance_records_filter_invalid`。
- 放假日：沒有刷卡也沒有假單的放假日預設不回傳；`includeDaysOff=true` 才列出。日型的行事曆備註在 `calendarRemark`（例如假日名稱），畫面在日型下方顯示。
- 一次請求的查詢次數固定：持有期間、刷卡（資料庫依持有期間與日彙總最早、最晚）、GD 行事曆、GD 假單各一次，只查這一頁的人，不逐人逐日查詢。
- 人員選單：`GET /api/v1/attendance-records/people` 列出範圍內持有過卡的人（含卡已隱藏與已不持卡的人，各有旗標），不分區間；畫面以它填人員下拉，不再從這一頁的名單推得。

## 資料範圍

- 支援 `own`、`unit`、`unit-tree`、`all`，套用方式與活動稽核相同：`unit`／`unit-tree` 是範圍單位**目前**的成員，人調走後由新單位的主管看到他過去的出勤。機制見 [資料範圍](../architecture/data-scopes.md)。
- 授權在但沒有可用範圍：200 與空名單；沒有功能授權：403 `feature_forbidden`。
- 預設：`workspace` 群組 `own`、`hr` 群組 `all`。組織勾選的主管另外自動取得所管單位含下層（[主管範圍](../architecture/data-scopes.md#主管範圍) `unit-tree`），與群組範圍 OR 合併；同時所屬但不是主管的單位不算，取消勾選即失效。主管仍要有 `attendance.records` 授權（`workspace` 已有）。

## 名單

- 查詢期間（到今天為止）持有卡的人：持有期間與某一天有交集，那一天才回傳。到職前、換卡空窗與離職後不列，免得判成沒刷卡。
- 只有姓名的持有人（新人的卡先以姓名指派）依附在卡號（`cardNumber`，`userId`、`account` 為 null、`leaveAvailable` 為 false），範圍是 `all` 且沒有單位篩選才列出；同卡同名的期間合併成一列，同名同姓的人不會混在一起，綁上帳號後併入該使用者。
- 沒有持有期間涵蓋的刷卡不屬於任何人，不在名單；在卡號對應頁以「待指派」處理（見 [出勤打卡](attendance.md#卡片)）。
- 在出勤紀錄隱藏的卡（見 [出勤打卡](attendance.md#在出勤紀錄隱藏)）只在範圍是 `all` 且沒有指定人員或卡號時略過其持有期間；不是隱私控制，本人與主管照常看得到。指定時照常列出，該人的期間全在隱藏的卡上時 `hidden` 為 true。

## 邊界狀態

- **今天**（台灣日期，回應的 `today`）：打卡機 23:52 才輸出當天的檔，出勤與異常一律改為無法判定（`DayInProgress`），刷卡照樣回傳，也不算遲到；放假與請假照常顯示。
- **未來**的日子不回傳；整段都在未來時名單為空。
- **GD 連不上**：`calendarAvailable`、`leaveAvailable` 為 false。日型退回週末放假、平日無法判定（`DayTypeUnknown`），所以平日不判遲到早退也不判異常；每天的假單是無法取得，刷卡照常顯示。
- **對不到公文帳號**（沒有 AD 帳號或 `Sys_User` 沒有）：該人 `leaveAvailable` 為 false，每天的假單是無法取得；沒刷卡的日子是無法判定（`LeaveUnavailable`），不是異常。
- **假單依日切分**：單據的原始起訖與當天 00:00～隔天 00:00 有交集就列在當天（跨日、半天都一樣），只並列、不比對時數；上午請假、下午才到照樣算遲到。

## 規則說明

- `GET /api/v1/attendance-rules`（授權同 `attendance.records`；全公司一套、不含個人資料，不套資料範圍）回傳每種日型的上下班時間與逐條規則。時間取自 `Attendance:Workday` 的目前設定，畫面不寫死。
- 條文只寫在 [AttendanceRules](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceRules.cs)，舉例的應下班由 `AttendanceEvaluator` 算出；改判定時一併改條文（`AttendanceRulesTests`）。
- 涵蓋：最早／最晚刷卡為上下班、出勤日依刷卡日期（過午夜算隔天）、彈性上班、遲到從彈性終點起算並改為表定下班、早退以秒計且沒有寬限、只有一筆刷卡、假單只並列不抵銷、日型來自公文系統行事曆、無法判定的情況、只列持卡日。

## 匯出 Excel

- `GET /api/v1/attendance-records/export`，參數與查詢相同（不含分頁）。handler 直接呼叫 `ListAttendanceRecords` 並取全部的人：區間上限、資料範圍、篩選、放假日與判定都同一份，看不到的人匯不出來；不另寫 procedure，查詢次數本來就固定。
- 回 `.xlsx`，檔名與舊系統相同：`出缺勤計算_匯出時間(起日to迄日).xlsx`（台灣時間，`filename*` UTF-8）；`/api` 回應一律不快取。
- 檔案與舊系統 EmpClock 的「出缺勤計算」一模一樣，HR 核對、列印與轉交的習慣不變；版面寫在 [AttendanceReportWorkbook](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceReportWorkbook.cs)，`ExportAttendanceRecordsTests` 逐格比對。
  - 單一工作表「出缺勤計算」，標楷體 13.875 點、置中（備註靠左）、列高 19.5、舊檔欄寬；沒有凍結窗格與篩選，時間與日期是文字。
  - 每人一段、每天一列，段後空兩列；段首寫姓名與卡號（使用者寫區間內持有的卡，換過卡以「、」相連），下面三列是「遲到(計)」「早退(計)」「異常(計)」天數，不到四天的人補列放小計。
  - 遲到的上班、早退的下班紅字，遲到、早退、異常欄打「＊」；備註是 `【核准】【特休假】單號〔起 ~ 迄〕`，出差寫【出差單】，多筆以「、」相連。流程狀態 `Send` 寫「流程中」，`Decide`、`Finish` 寫「核准」（畫面同一套字樣）。
- 與舊檔不同的只有資料本身：列哪些日子、怎麼判遲到早退異常都照畫面（含放假日勾選、未來的日子不列、只有一筆刷卡只寫在上班）；假單無法取得時備註寫「無法取得」。
- 不另設 `attendance.records.export`：匯出的就是畫面看得到的資料，分權只會讓主管與本人多一道設定；大量帶出靠稽核與每人每分鐘 5 次的 rate limit 控制。
- 稽核：先寫 `attendance.records.export`（「查閱與匯出」分類，記區間、人員或卡號、單位、是否含放假日、人數，不記名單）再產生檔案，寫不進去就不匯出；被拒絕的區間與權限不留紀錄。
- 版面固定，不走共用的 [Spreadsheet](../../backend/src/AiNexus.Platform/Export/Spreadsheet.cs)（它固定加標題樣式、凍結、篩選與微軟正黑體）；文字仍經它的 `Clean` 移除 XML 不允許的字元。
