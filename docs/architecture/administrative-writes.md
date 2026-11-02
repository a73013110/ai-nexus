# 行政寫入

結論：管理端的寫入預設用 [`AdministrativeWrite`](../../backend/src/AiNexus.Features/Admin/AccessControl/AdministrativeWrite.cs)。它是資料稽核：記下 handler 實際改了什麼（前後值），變更與快照一起提交或一起回滾。只有下表其他三種情況例外。

## 該用哪一種

每個端點都要宣告 `.Audited(動作)`，它決定「這個請求算什麼動作」（[ADR 0023](../decisions/0023-audit-standard.md)）。下表決定「改了什麼」怎麼記。

| 寫入 | 做法 | 範例 |
| --- | --- | --- |
| 使用者自己的資料 | handler 直接 `SaveChanges` | [`SaveProject`](../../backend/src/AiNexus.Features/Work/Projects/Management/SaveProject.cs) |
| 管理端的資料（角色、選單、組織、卡片、郵件設定） | `AdministrativeWrite` | [`CreateCard`](../../backend/src/AiNexus.Features/Office/Attendance/Cards/CreateCard.cs) |
| 行政資料，寫在 procedure | 程式開交易再呼叫 | [`CreateModelPrice`](../../backend/src/AiNexus.Features/Admin/Billing/Prices/CreateModelPrice.cs)，見 [SQL 物件](sql-objects.md) |
| 同時改外部系統 | 自己管理兩邊的提交順序 | [`UpdateCalendarDay`](../../backend/src/AiNexus.Features/Office/Attendance/Calendar/UpdateCalendarDay.cs) |

## 它做了什麼

開交易、取宣告的鎖、讀前值；提交時 `SaveChanges`、讀後值，再一起提交（原因見 [ADR 0032](../decisions/0032-administrative-write-begin-commit.md)）。

- 沒提交就離開（回傳錯誤、拋例外）一律回滾，並留下含前值的失敗稽核。
- 回成功卻沒提交時拋例外回 503，不會回成功卻沒存。
- 與其他寫入撞上（版本不符、唯一索引、死結）回 409 `admin_write_conflict`，不自動重試。
- 取鎖逾時回 503 `admin_write_busy`，所以耗時的操作放在交易外面，使用者端的高頻寫入不用它。

## 怎麼寫

```csharp
var begun = await write.BeginAsync(CardRules.Target(db, id), ct);
if (!begun.IsSuccess) return begun.Error;
await using var transaction = begun.Value;

var card = await db.Cards.SingleOrDefaultAsync(x => x.Id == id, ct);
if (card is null) return AttendanceErrors.CardNotFound;
db.ExpectRowVersion(card, request.RowVersion);
card.Note = request.Note.TrimToNull();

return await transaction.CommitAsync(ct);
```

- **寫入對象 `AuditTarget`**：功能、資源 ID、快照怎麼讀。同一資源有多個 slice 時放在 `<Resource>Rules.Target`，稽核形狀才不會各寫各的；只有一個 slice 用時直接寫在 handler。
- **快照**：只投影要留證的欄位，鍵名必須在 [`AuditRedactor`](../../backend/src/AiNexus.Platform/Auditing/AuditRedactor.cs) 白名單內。照常寫 `AsNoTracking`；不要用 `FindAsync`：它先查追蹤中的實體，會記到未存的值。
- **資料庫產生的值**：提交後才讀得到（例如新的 `RowVersion`），回應的 DTO 在 `CommitAsync` 之後才組。

## 交易佔多久

佔資源的只有開始到提交之間，所以耗時的工作（外部呼叫、讀檔、雜湊）放在 `BeginAsync` 之前。

- **提交之後不佔任何東西**：`CommitAsync` 不論結果都已放掉交易、鎖與連線，之後重讀 DTO 不必擔心。
- **一律 `await using var`**：改成 `using (...) { }` 區塊只多一層巢狀，開始到提交的長度不會變短。
- **列鎖只在提交時才有**：EF 的變更都在 `CommitAsync` 才送出。提交前佔的是連線與宣告的鎖，RCSI 下不擋讀取。
- **佔太久會記錄**：佔到 `AccessControl:WriteLockTimeoutSeconds`（別人等鎖的上限）時記 `admin_write.held_too_long`。

## 防覆蓋：`RowVersion`

使用者會看著畫面改的表都加。實體實作 [`IRowVersioned`](../../backend/src/AiNexus.Data/App/IRowVersioned.cs)，組態 `IsRowVersion().HasConversion<byte[]>()`；DTO 帶出，PUT 放 body、DELETE 放 `?rowVersion=`。

- `ExpectRowVersion` 比對的是使用者讀到的版本，不是 handler 剛查的。內容沒變時 EF 不送 UPDATE，也就不比對。
- 會影響別筆資料的確認（例如接手會結束別人的期間）帶被影響那筆的版本，使用者確認的才是實際被改的內容。

## 要不要鎖

預設不鎖。依序問：

1. 只改一列？`RowVersion` 就夠。
2. 規則能寫成唯一索引或條件式 UPDATE？用它們，撞上就回 409。
3. 規則跨多列又沒有約束可用（例如同一張卡的期間不可重疊）？才以 `AuditTarget.Locking(名稱)` 宣告，範圍取規則涵蓋的最小資源。

- 鎖名集中在模組的 `<Module>Locks`，例如 [`AttendanceLocks`](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceLocks.cs)。多把鎖依名稱排序後才取，不會互相死結。
- 規則要靠鎖才成立、又有不經這裡的寫入者時，對方在交易的第一步也取同一把鎖，例如 [`PunchImporter`](../../backend/src/AiNexus.Features/Office/Attendance/Imports/PunchImporter.cs)。
- 宣告了鎖就要有並行測試，例如 [`CardConcurrencyTests`](../../backend/tests/AiNexus.IntegrationTests/Attendance/CardConcurrencyTests.cs)。

## 遷移中：`ExecuteAsync`

尚未遷移的模組仍用 callback 版：一個模組一把鎖，交易內前後各檢查一次權限（[ADR 0031](../decisions/0031-administrative-write-scoped-locks.md)）。帳號與存取控制要在交易內檢查 lockout，遷移時另行設計；其他模組遷移時逐一列出跨列規則，再決定鎖。新程式一律用 `BeginAsync`。
