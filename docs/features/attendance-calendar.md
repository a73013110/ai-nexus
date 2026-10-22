# 假日設定

結論：HR 在本系統查詢與修改公文系統（GD）的假日行事曆 `dbo.Pub_Days`。查詢走唯讀的 `Databases:Gd`，回寫走另一個最小權限的可寫入連線 `Databases:GdCalendar`；為什麼允許回寫見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md#假日與假單公文系統)。前端頁面目前仍是佔位頁。

## 授權

- `attendance.calendar`：查詢。`attendance.calendar.maintain`：回寫，端點兩者都要有。
- 兩者都在 `AdministrativeFeatures`：測試身分可以查詢，回寫回 403 `test_admin_read_only`。

## 查詢

- `GET /api/v1/attendance-calendar-days?from=2026-10-01&to=2026-10-31`，起訖都含，最長 366 天；缺一端、顛倒或超過回 400 `attendance_calendar_range_invalid`。
- 只列 GD 有的日期，不補空白日：回寫只能改既有日期。
- GD 未設定或連不上回 503 `attendance_gd_unavailable`，不回空清單（空清單會被當成「整月都上班」）。

## 回寫

- `PUT /api/v1/attendance-calendar-days/{date}`，body `{ "morningOff": true, "afternoonOff": true, "remark": "國慶日" }`，回傳修改後的值。
- 只 `UPDATE` 既有日期的 `AM`、`PM`、`hReMark` 與 `UserId`。GD 沒有這一天回 404 `attendance_calendar_day_not_found`，不新增、不刪除；日期列由公文系統建立。
- `UserId` 寫操作者的公文帳號 `Sys_User.ID`（`Sys_User.ACCOUNT = Users.AdAccount`，經唯讀連線查）。停用列不算（全部停用時才退回全部列）；沒有 AD 帳號、`Sys_User` 沒有或仍對到多筆回 403 `attendance_gd_account_unmapped`，不退回寫 `sys`。
- 備註去掉前後空白、空白存成 null；驗證只擋超過 255 字與控制字元。`hReMark` 是 varchar(255)：上限是位元組，取決於 GD 欄位的定序（Big5 中文每字 2 位元組，約 127 字），字碼頁沒有的字（罕用字、表情符號）會被換成問號。本系統不估算，而是在同一個交易寫入後讀回逐字比對，存不下或走樣就回滾並回 400 `attendance_calendar_remark_invalid`。
- 沒有設定 `Databases:GdCalendar`：網站照常啟動、查詢照常，回寫回 503 `attendance_calendar_writeback_not_configured`。可寫入連線連不上、逾時或權限不足回 503 `attendance_gd_unavailable`（日誌 3409）。

## 稽核與跨資料庫的順序

- 動作 `admin.attendance_calendar_day`，`resourceKey` 是日期；成功的紀錄含前後的 `morningOff`、`afternoonOff`、`calendarRemark`、`gdUserId`，被拒絕的嘗試留錯誤代碼。活動稽核目前併在「出勤卡號異動」篩選。
- GD 與本系統無法同一個交易，順序是：以 `UPDLOCK, HOLDLOCK` 鎖定這一天並讀前值 → 更新（未提交）→ 寫本系統稽核 → 提交 GD。
  - 稽核寫不進去：GD 回滾，什麼都沒改。不會有「GD 改了卻沒有稽核」。
  - GD 提交失敗：已有一筆 `saved`，再補一筆 `attendance_gd_unavailable` 並記日誌 3410。提交途中斷線時 GD 可能其實已提交，重新查詢確認。
- 鎖持有到稽核寫完（一次本機 SQL 寫入）；並發修改同一天依序進行，各自的前值都正確。

## 部署

- `Databases:GdCalendar` 指向與 `Databases:Gd` 相同的伺服器與資料庫，但用另一個帳號；`ReadOnly` 維持 `false`（`Gd` 仍必須是 `true`，啟動時檢查）。一般設定填 `Server`、`Name`、`TrustServerCertificate`，秘密檔填 `User`、`Password`（或 `IntegratedSecurity=true`）。逾時沿用 `Integrations:Gd:CommandTimeoutSeconds`，等不到公文系統的鎖也算逾時。
- DBA 建立最小權限帳號（名稱自訂），只給：

  ```sql
  GRANT SELECT ON dbo.Pub_Days TO ainexus_calendar;
  GRANT UPDATE (AM, PM, hReMark, UserId) ON dbo.Pub_Days TO ainexus_calendar;
  ```

  不需要 `Sys_User`（帳號換算走唯讀連線），不給 INSERT、DELETE 或其他表。這組權限已在本機以同結構驗證過讀前值（含鎖定提示）、更新與讀回；INSERT、DELETE、改 `hDate` 都被拒。
- 唯讀的 `Databases:Gd` 帳號的權限見 [出勤與公文系統 GD](attendance-gd.md#唯讀帳號的權限)。
