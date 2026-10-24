# 出勤與公文系統 GD

結論：出勤從公文系統 GD 讀取假日、假單與出差單，並回寫假日；[組織](organization.md#從公文系統匯入)從 GD 匯入部門與主管；三者與 [公文與校務系統整合](integrations.md) 共用同一個 `Databases:Gd`。以程式查詢 GD 一律經 Integrations 的 [`GdAccess`](../../backend/src/AiNexus.Features/Admin/Integrations/Gd/GdAccess.cs)（帳號對應在同一資料夾的 `GdAccounts`），回寫經出勤的 [`GdCalendarWriter`](../../backend/src/AiNexus.Features/Office/Attendance/Gd/GdCalendarWriter.cs) 並向 `GdAccess` 取連線，GD 未設定或不能用時降級、不拋例外。GD 的可用性是即時監控的子系統 `gd`。為什麼讀寫 GD 見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md#假日與假單公文系統)，回寫的規則見 [假日設定](attendance-calendar.md)。

## 連線與帳號權限

- `appsettings.json` 預設連 GD 測試庫（192.168.2.207），開發與測試不會改到正式資料；正式環境在部署設定把 `Databases:Gd:Server` 覆寫成 CARD。
- `Databases:Gd` 預設可讀寫（`ReadOnly=false`），直接用既有帳號即可。寫入範圍由程式限定：只 `UPDATE` `Pub_Days` 既有日期的四欄，不新增、不刪除、不碰其他表。
- 只想讀、不想回寫時設 `ReadOnly=true`：假日回寫回 503 `attendance_gd_read_only`，查詢照常；連線另帶 `ApplicationIntent=ReadOnly`。這只是本系統的開關與連線意圖，要在資料庫端擋寫入得另用沒有 UPDATE 權限的帳號。
- 另建最小權限帳號時的最低需求如下；不給 `db_datareader`、其他表、INSERT、DELETE 或 procedure：

```sql
-- Integrations：授權 view，契約與角色見 deploy/sql/authorized-views.sql
GRANT SELECT ON OBJECT::ainexus.AuthorizedRecords TO ainexus_reader;
GRANT SELECT ON OBJECT::ainexus.AuthorizedRecordHistory TO ainexus_reader;
-- 出勤與組織匯入：假日、帳號對應（主管也經它對到使用者）、假單（含跨區間與假別）、出差單
GRANT SELECT ON dbo.Pub_Days TO ainexus_reader;
GRANT SELECT ON dbo.Sys_User TO ainexus_reader;
GRANT SELECT ON dbo.Form_Leave TO ainexus_reader;
GRANT SELECT ON dbo.Form_Leave_Group TO ainexus_reader;
GRANT SELECT ON dbo.Form_LeaveKind TO ainexus_reader;
GRANT SELECT ON dbo.Form_Travel TO ainexus_reader;
-- 組織匯入：只給用到的欄位，登記桌 Email、機關等不讀
GRANT SELECT (DEP_NO, DEP_NAME, DEP_CHIEF, DEP_ACHIEF, PARENT_NO, OrderBy, [Disable]) ON dbo.Sys_User_DEPT_BAS TO ainexus_reader;
-- 假日回寫（設 ReadOnly=true 時不需要）：直接給 GD 帳號（名稱自訂），不放進讀取角色
GRANT UPDATE (AM, PM, hReMark, UserId) ON dbo.Pub_Days TO ainexus_gd;
```

- `Sys_User` 是公文系統的帳號表，帳號對應讀 `ID`、`ACCOUNT`、`Disable`（排除停用帳號），整表授權。
- 只用其中一個功能時，可只給該段。少了需要的讀取授權，該查詢被拒（日誌 3411），監控轉為 degraded，組織匯入回 503 `organization_import_rejected`；少了 UPDATE，回寫回 503 `attendance_gd_unavailable`（日誌 3409）。新增讀寫的表時同步更新這份清單。
- 這組回寫權限曾在本機以同結構驗證過讀前值（含鎖定提示）、更新與讀回；INSERT、DELETE、改 `hDate` 都被拒。

## 降級

`GdAccess` 在未設定、連不上、逾時或查詢被拒時回傳 null，由呼叫端決定：假單查詢回「無法取得」而不是空清單（出勤據此標示，不把每天判成異常）；假日設定回 503 `attendance_gd_unavailable`。呼叫端取消與程式錯誤照常拋出，不算 GD 失敗。

## 監控

- 判定見 [監控子系統](../architecture/monitoring-sources.md)。健康狀態取自 `GdAccess` 每次存取的成敗（時間與原因分類，不含連線字串或例外訊息），只在記憶體，重啟後歸零。
- 監控讀取時若 30 秒內沒有任何存取，先做一次 `SELECT 1` 探測（3 秒時限，低於監控讀取的 5 秒逾時）。GD 只在有人開出勤、假日頁或預覽組織匯入時才被查，只靠實際存取，沒人用的時段會停在舊結果；探測只在有人看監控時發生，有人在用時不多打 GD。未設定 `Databases:Gd` 時不探測，顯示 idle「未設定」。
- 原因分類：連不上（網路、登入、資料庫離線等）、逾時、查詢被拒（SQL 錯誤 207、208、229、230、4104：表或欄位不存在、沒有權限）。前兩種是整個不能用，failing；被拒只影響部分查詢，`SELECT 1` 測不出來，所以維持 degraded 到最後一次被拒後 1 小時。
- 「假日回寫」依 `ReadOnly` 顯示可寫入或唯讀，不另外探測寫入權限；回寫失敗當場回 503 並記日誌 3409、3410，不影響監控判定。
- 日誌：3407 不能用（含原因）、3408 恢復，只在轉換或原因改變時記；3411 查詢被拒，每段被拒期間記一次。
