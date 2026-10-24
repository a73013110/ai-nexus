# 出勤紀錄

結論：`GET /api/v1/attendance-records?from=2026-10-01&to=2026-10-31`（頁面 `attendance.records`）回傳資料範圍內每人每天的判定結果、上下班刷卡、應下班、遲到與早退秒數，以及當天的假單與出差單；同一組條件可[匯出 Excel](#匯出-excel)，頁首「判定規則」面板顯示[規則說明](#規則說明)。判定規則見 [出勤打卡](attendance.md#出勤判定)，取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)；回應形狀是 `contracts/openapi.json` 的 `AttendanceRecordsDto`。頁首的問號可開啟操作導覽。

## 查詢

- 起訖是出勤日，都含、必填，最長 366 天（整年含閏年；一次匯出整年或看一個人的整年）；缺一端、顛倒或超過回 400 `attendance_records_range_invalid`。
- 依人分頁：`offset`、`limit`（預設 20、最多 50），依姓名排序，`total` 是符合條件的人數。區間放寬到一年後，一頁的列數是人數 × 天數，不分頁會過大；每人的天數不切開。
- 篩選：`userId`、`card`（沒有帳號的資料，見[名單](#名單)）；`unitId` 是該單位及下層的目前成員。都與資料範圍取交集，範圍外的人只是查不到，不回錯誤。格式不對（卡號過長、分頁超出）回 400 `attendance_records_filter_invalid`。
- 放假日：沒有刷卡也沒有假單的放假日預設不回傳；`includeDaysOff=true` 才列出。日型的行事曆備註在 `calendarRemark`（例如假日名稱），畫面在日型下方顯示。
- 一次請求的查詢次數固定：持有期間、刷卡（資料庫依持有期間與日彙總最早、最晚）、GD 行事曆、GD 假單各一次，只查這一頁的人，不逐人逐日查詢。
- 人員選單：`GET /api/v1/attendance-records/people` 列出範圍內持有過員工卡的人（含不列入出勤的人與已不持卡的人，各有旗標），不分區間；畫面以它填人員下拉，不再從這一頁的名單推得。

## 資料範圍

- 支援 `own`、`unit`、`unit-tree`、`all`，套用方式與活動稽核相同：`unit`／`unit-tree` 是範圍單位**目前**的成員，人調走後由新單位的主管看到他過去的出勤。機制見 [資料範圍](../architecture/data-scopes.md)。
- 授權在但沒有可用範圍：200 與空名單；沒有功能授權：403 `feature_forbidden`。
- 預設：`workspace` 群組 `own`、`hr` 群組 `all`。組織勾選的主管另外自動取得所管單位含下層（[主管範圍](../architecture/data-scopes.md#主管範圍) `unit-tree`），與群組範圍 OR 合併；同時所屬但不是主管的單位不算，取消勾選即失效。主管仍要有 `attendance.records` 授權（`workspace` 已有）。

## 名單

- 查詢期間（到今天為止）持有員工卡的使用者：持有期間與某一天有交集，那一天才回傳。到職前、換卡空窗與離職後不列，免得判成沒刷卡。
- 訪客卡不在名單。
- 沒有帳號的資料依附在卡號（`cardNumber`，`userId`、`account` 為 null、`leaveAvailable` 為 false），範圍是 `all` 且沒有單位篩選才列出：
  - 只有姓名的持有人（新人的卡先以姓名指派）：同卡同名的期間合併成一列，同名同姓的人不會混在一起；綁上帳號後併入該使用者。
  - 未歸戶卡號（`unattributed`）：`includeUnattributed=true` 或指定 `card` 才列，排在最後，只列有刷卡的日子。
- 不列入出勤（`attendance.RosterExclusions`，在卡號對應頁的「不列入出勤」檢視管理，見 [出勤打卡](attendance.md#不列入出勤)）只在範圍是 `all`、沒有指定人員、也沒有 `includeExcluded=true` 時隱藏。它只是 HR 看全公司時隱藏不重要的人，不是隱私控制：本人與主管照常看得到；有出現時 `excluded` 為 true。沒有帳號的資料依卡號標（`attendance.RosterCardExclusions`）。

## 邊界狀態

- **今天**（台灣日期，回應的 `today`）：打卡機 23:52 才輸出當天的檔，出勤與異常一律改為無法判定（`DayInProgress`），刷卡照樣回傳，也不算遲到；放假與請假照常顯示。
- **未來**的日子不回傳；整段都在未來時名單為空。
- **GD 連不上**：`calendarAvailable`、`leaveAvailable` 為 false。日型退回週末放假、平日無法判定（`DayTypeUnknown`），所以平日不判遲到早退也不判異常；每天的假單是無法取得，刷卡照常顯示。
- **對不到公文帳號**（沒有 AD 帳號或 `Sys_User` 沒有）：該人 `leaveAvailable` 為 false，每天的假單是無法取得；沒刷卡的日子是無法判定（`LeaveUnavailable`），不是異常。
- **假單依日切分**：單據的原始起訖與當天 00:00～隔天 00:00 有交集就列在當天（跨日、半天都一樣），只並列、不比對時數；上午請假、下午才到照樣算遲到。

## 規則說明

- `GET /api/v1/attendance-rules`（授權同 `attendance.records`；全公司一套、不含個人資料，不套資料範圍）回傳每種日型的上下班時間與逐條規則。時間取自 `Attendance:Workday` 的目前設定，畫面不寫死；頁面與匯出的「說明」工作表用同一份。
- 條文只寫在 [AttendanceRules](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceRules.cs)，舉例的應下班由 `AttendanceEvaluator` 算出；改判定時一併改條文（`AttendanceRulesTests`）。
- 涵蓋：最早／最晚刷卡為上下班、出勤日依刷卡日期（過午夜算隔天）、彈性上班、遲到從彈性終點起算並改為表定下班、早退以秒計且沒有寬限、只有一筆刷卡、假單只並列不抵銷、日型來自公文系統行事曆、無法判定的情況、只列持卡日。

## 匯出 Excel

- `GET /api/v1/attendance-records/export`，參數與查詢相同（不含分頁）。handler 直接呼叫 `ListAttendanceRecords` 並取全部的人：區間上限、資料範圍、篩選、放假日與判定都同一份，看不到的人匯不出來；不另寫 procedure，查詢次數本來就固定。
- 回 `.xlsx`，檔名 `出勤紀錄_起日-迄日_匯出時間.xlsx`（台灣時間，`filename*` UTF-8）；`/api` 回應一律不快取。
- 工作表：
  - **出勤紀錄**：每人每天一列，姓名、帳號或卡號每列都有（可篩選），日型旁是行事曆備註；日期、時刻與遲到早退時數是 Excel 的日期時間值（可加總）。遲到的上班、早退的下班、異常以深紅色標示，列印時每人一頁。
  - **統計**：每人的天數、各狀態天數、遲到與早退的天數與合計。
  - **說明**：查詢區間、匯出時間、條件（含是否列出放假日）、GD 無法取得的提示，以及上方的規則說明。
- 對照舊系統 EmpClock「出缺勤計算」：上班、下班、遲到、早退照舊，遲到早退由「＊」改為時數；異常併入「狀態」，備註拆成「說明」（判定原因）與「假單與出差」；舊版只在每人第一列寫姓名、下方塞「遲到(計)」等小計列與空白列，篩選後看不出是誰，改為平面表格加「統計」工作表。卡號改為帳號（一人可能多張卡）。
- 不另設 `attendance.records.export`：匯出的就是畫面看得到的資料，分權只會讓主管與本人多一道設定；大量帶出靠稽核與每人每分鐘 5 次的 rate limit 控制。
- 稽核：先寫 `attendance.records.export`（「查閱與匯出」分類，記區間、人員或卡號、單位、是否含不列入、未歸戶與放假日、人數，不記名單）再產生檔案，寫不進去就不匯出；被拒絕的區間與權限不留紀錄。
- 試算表由共用的 [Spreadsheet](../../backend/src/AiNexus.Platform/Export/Spreadsheet.cs) 產生（標題列凍結與篩選、欄寬、日期時間格式、分頁、列印重複標題、微軟正黑體），其他列表匯出 Excel 時沿用。
