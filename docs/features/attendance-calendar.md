# 假日設定

結論：HR 在本系統查詢與修改公文系統（GD）的假日行事曆 `dbo.Pub_Days`。查詢與回寫用同一個 `Databases:Gd`，寫入範圍由程式限定；為什麼允許回寫見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md#假日與假單公文系統)。頁首與編輯對話框的問號可開啟操作導覽，介紹月曆、清單與四個放假選項。

## 授權

- `attendance.calendar`：查詢。`attendance.calendar.maintain`：回寫，端點兩者都要有。

## 查詢

- `GET /api/v1/attendance-calendar-days?from=2026-10-01&to=2026-10-31`，起訖都含，最長 366 天；缺一端、顛倒或超過回 400 `attendance_calendar_range_invalid`。
- 只列 GD 有的日期，不補空白日：回寫只能改既有日期。
- GD 未設定或連不上回 503 `attendance_gd_unavailable`，不回空清單（空清單會被當成「整月都上班」）。

## 畫面

- 預設是月曆：放假的半天畫在格子的上半（上午）或下半（下午），GD 沒有的日期標「未建立」且不能點；「清單」是同一份資料的逐日表格（網址 `view=list`），手機上讀備註較方便。
- 編輯對話框把上午、下午兩個布林呈現為四選一（上班日／上午放假／下午放假／放假），存檔後把伺服器回傳值換進月份，不重讀。

## 回寫

- `PUT /api/v1/attendance-calendar-days/{date}`，body `{ "morningOff": true, "afternoonOff": true, "remark": "國慶日" }`，回傳修改後的值。
- 只 `UPDATE` 既有日期的 `AM`、`PM`、`hReMark` 與 `UserId`。GD 沒有這一天回 404 `attendance_calendar_day_not_found`，不新增、不刪除；日期列由公文系統建立。
- `UserId` 寫操作者的公文帳號 `Sys_User.ID`（`Sys_User.ACCOUNT = Users.AdAccount`）。停用列不算（全部停用時才退回全部列）；沒有 AD 帳號、`Sys_User` 沒有或仍對到多筆回 403 `attendance_gd_account_unmapped`，不退回寫 `sys`。
- 備註去掉前後空白、空白存成 null；驗證只擋超過 255 字與控制字元。`hReMark` 是 varchar(255)：上限是位元組，取決於 GD 欄位的定序（Big5 中文每字 2 位元組，約 127 字），字碼頁沒有的字（罕用字、表情符號）會被換成問號。本系統不估算，而是在同一個交易寫入後讀回逐字比對，存不下或走樣就回滾並回 400 `attendance_calendar_remark_invalid`。
- `Databases:Gd` 設為 `ReadOnly=true`：查詢照常，回寫回 503 `attendance_gd_read_only`，不連 GD。未設定、連不上、逾時或沒有 UPDATE 權限回 503 `attendance_gd_unavailable`（日誌 3409）。逾時沿用 `Databases:Gd:CommandTimeoutSeconds`，等不到公文系統的鎖也算逾時。

## 稽核與跨資料庫的順序

- 動作 `admin.calendar_day`，`resourceKey` 是日期；成功的紀錄含前後的 `morningOff`、`afternoonOff`、`calendarRemark`、`gdUserId`，被拒絕的嘗試留錯誤代碼。活動稽核以「假日設定異動」篩選。
- GD 與本系統無法同一個交易，順序是：以 `UPDLOCK, HOLDLOCK` 鎖定這一天並讀前值 → 更新（未提交）→ 寫本系統稽核 → 提交 GD。
  - 稽核寫不進去：GD 回滾，什麼都沒改。不會有「GD 改了卻沒有稽核」。
  - GD 提交失敗：已有一筆 `saved`，再補一筆 `attendance_gd_unavailable` 並記日誌 3410。提交途中斷線時 GD 可能其實已提交，重新查詢確認。
- 鎖持有到稽核寫完（一次本機 SQL 寫入）；並發修改同一天依序進行，各自的前值都正確。

## 部署

連線與帳號權限（含只讀不回寫的設定）見 [出勤與公文系統 GD](attendance-gd.md#連線與帳號權限)。
