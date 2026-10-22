# 出勤與公文系統 GD

結論：出勤從公文系統 GD 唯讀讀取假日、假單與出差單，與 [公文與校務系統唯讀整合](integrations.md) 共用 `Databases:Gd`；查詢一律經 [`GdAccess`](../../backend/src/AiNexus.Features/Staff/Attendance/Gd/GdAccess.cs)，GD 未設定或不能用時降級、不拋例外。GD 的可用性是即時監控的子系統 `attendance-gd`。為什麼讀 GD 見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md#假日與假單公文系統)，假日回寫與它的可寫入帳號見 [假日設定](attendance-calendar.md)。

## 唯讀帳號的權限

`Databases:Gd` 的帳號由 Integrations 與出勤共用。現有的唯讀帳號已能讀全庫；下列 SELECT 是另建最小權限帳號時的最低需求，不給 `db_datareader`、其他表、寫入或 procedure：

```sql
-- Integrations：授權 view，契約與角色見 deploy/sql/authorized-views.sql
GRANT SELECT ON OBJECT::ainexus.AuthorizedRecords TO ainexus_reader;
GRANT SELECT ON OBJECT::ainexus.AuthorizedRecordHistory TO ainexus_reader;
-- 出勤：假日、帳號對應、假單（含跨區間與假別）、出差單
GRANT SELECT ON dbo.Pub_Days TO ainexus_reader;
GRANT SELECT ON dbo.Sys_User TO ainexus_reader;
GRANT SELECT ON dbo.Form_Leave TO ainexus_reader;
GRANT SELECT ON dbo.Form_Leave_Group TO ainexus_reader;
GRANT SELECT ON dbo.Form_LeaveKind TO ainexus_reader;
GRANT SELECT ON dbo.Form_Travel TO ainexus_reader;
```

- `Sys_User` 是公文系統的帳號表，帳號對應讀 `ID`、`ACCOUNT`、`Disable`（排除停用帳號），整表授權。
- 只用其中一個功能時，可只給該段。少了出勤需要的授權，該查詢被拒（日誌 3411），監控轉為 degraded。
- `ApplicationIntent=ReadOnly` 只是連線意圖，不能取代這些權限。新增讀取的表時同步更新這份清單。

## 降級

`GdAccess` 在未設定、連不上、逾時或查詢被拒時回傳 null，由呼叫端決定：假單查詢回「無法取得」而不是空清單（出勤據此標示，不把每天判成異常）；假日設定回 503 `attendance_gd_unavailable`。呼叫端取消與程式錯誤照常拋出，不算 GD 失敗。

## 監控

- 判定見 [監控子系統](../architecture/monitoring-sources.md)。健康狀態取自 `GdAccess` 每次存取的成敗（時間與原因分類，不含連線字串或例外訊息），只在記憶體，重啟後歸零。
- 監控讀取時若 30 秒內沒有任何存取，先做一次 `SELECT 1` 探測（3 秒時限，低於監控讀取的 5 秒逾時）。GD 只在有人開出勤或假日頁時才被查，只靠實際存取，沒人用的時段會停在舊結果；探測只在有人看監控時發生，有人在用時不多打 GD。未設定 `Databases:Gd` 時不探測，顯示 idle「未設定」。
- 原因分類：連不上（網路、登入、資料庫離線等）、逾時、查詢被拒（SQL 錯誤 207、208、229、230、4104：表或欄位不存在、沒有權限）。前兩種是整個不能用，failing；被拒只影響部分查詢，`SELECT 1` 測不出來，所以維持 degraded 到最後一次被拒後 1 小時。
- 可寫入的 `Databases:GdCalendar` 只顯示是否設定，不另立來源也不連線：同一個 GD，只在 HR 回寫時使用，失敗當場回 503 並記日誌 3409、3410。
- 日誌：3407 不能用（含原因）、3408 恢復，只在轉換或原因改變時記；3411 查詢被拒，每段被拒期間記一次。
