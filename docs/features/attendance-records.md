# 出勤紀錄

結論：`GET /api/v1/attendance-records?from=2026-10-01&to=2026-10-31`（頁面 `attendance.records`）回傳資料範圍內每人每天的判定結果、上下班刷卡、應下班、遲到與早退秒數，以及當天的假單與出差單。判定規則見 [出勤打卡](attendance.md#出勤判定)，取捨見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md)；回應形狀是 `contracts/openapi.json` 的 `AttendanceRecordsDto`。前端頁面目前仍是佔位頁。

## 查詢

- 起訖是出勤日，都含、必填，最長 31 天（畫面一次看一個月，也限制 GD 假單查詢的大小）；缺一端、顛倒或超過回 400 `attendance_records_range_invalid`。
- 依人分組，不分頁：名單是公司持卡人（數百人），畫面要整月逐人顯示，回應是人數 × 天數的小列。人數大到回應過大時再依人分頁。
- 篩選：`userId`；`unitId` 是該單位及下層的目前成員。兩者都與資料範圍取交集，範圍外的人只是查不到，不回錯誤。
- 一次請求的查詢次數固定：持有期間、刷卡（資料庫依人與日彙總最早、最晚）、GD 行事曆、GD 假單各一次，不逐人逐日查詢。

## 資料範圍

- 支援 `own`、`unit`、`unit-tree`、`all`，套用方式與活動稽核相同：`unit`／`unit-tree` 是範圍單位**目前**的成員，人調走後由新單位的主管看到他過去的出勤。機制見 [資料範圍](../architecture/data-scopes.md)。
- 授權在但沒有可用範圍：200 與空名單；沒有功能授權：403 `feature_forbidden`。
- 預設：`workspace` 群組 `own`、`hr` 群組 `all`。組織勾選的主管另外自動取得所管單位含下層（[主管範圍](../architecture/data-scopes.md#主管範圍) `unit-tree`），與群組範圍 OR 合併；同時所屬但不是主管的單位不算，取消勾選即失效。主管仍要有 `attendance.records` 授權（`workspace` 已有）。

## 名單

- 查詢期間（到今天為止）持有員工卡的使用者：持有期間與某一天有交集，那一天才回傳。到職前、換卡空窗與離職後不列，免得判成沒刷卡。
- 訪客卡、只有姓名還沒有帳號的持有人不在名單；綁上帳號後即出現。
- 不列入出勤（`attendance.RosterExclusions`）只在範圍是 `all`、沒有指定 `userId`、也沒有 `includeExcluded=true` 時隱藏。它只是 HR 看全公司時隱藏不重要的人，不是隱私控制：本人與主管照常看得到；有出現時 `excluded` 為 true。管理 API 尚未提供。

## 邊界狀態

- **今天**（台灣日期，回應的 `today`）：打卡機 23:52 才輸出當天的檔，出勤與異常一律改為無法判定（`DayInProgress`），刷卡照樣回傳，也不算遲到；放假與請假照常顯示。
- **未來**的日子不回傳；整段都在未來時名單為空。
- **GD 連不上**：`calendarAvailable`、`leaveAvailable` 為 false。日型退回週末放假、平日無法判定（`DayTypeUnknown`），所以平日不判遲到早退也不判異常；每天的假單是無法取得，刷卡照常顯示。
- **對不到公文帳號**（沒有 AD 帳號或 `Sys_User` 沒有）：該人 `leaveAvailable` 為 false，每天的假單是無法取得；沒刷卡的日子是無法判定（`LeaveUnavailable`），不是異常。
- **假單依日切分**：單據的原始起訖與當天 00:00～隔天 00:00 有交集就列在當天（跨日、半天都一樣），只並列、不比對時數；上午請假、下午才到照樣算遲到。
