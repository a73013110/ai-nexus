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
- `CommitAsync` 與其他寫入撞上（版本不符、唯一索引、死結）回 409 `admin_write_conflict`，不自動重試。提交前的 `ExecuteUpdate` 只可能是死結犧牲者（會撞約束的都在鎖內或走提交），回可重試的 503。
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
- **資料庫產生的值**：提交後才讀得到（例如新的 `RowVersion`），回應的 DTO 用 `CommitAsync(() => dto, ct)` 在提交成功後才組；還要查資料庫時，先判斷提交成功再查。

## 交易佔多久

佔資源的只有開始到提交之間，所以耗時的工作（外部呼叫、讀檔、雜湊）放在 `BeginAsync` 之前。

- **提交之後不佔任何東西**：`CommitAsync` 不論結果都已放掉交易、鎖與連線，之後重讀 DTO 不必擔心。
- **一律 `await using var`**：改成 `using (...) { }` 區塊只多一層巢狀，開始到提交的長度不會變短。
- **列鎖只在提交時才有**：EF 的變更都在 `CommitAsync` 才送出。提交前佔的是連線與宣告的鎖，RCSI 下不擋讀取。
- **佔太久會記錄**：佔到 `AccessControl:WriteLockTimeoutSeconds`（別人等鎖的上限）時記 `admin_write.held_too_long`。

## 防覆蓋：`RowVersion`

使用者會看著畫面改的表都加。實體實作 [`IRowVersioned`](../../backend/src/AiNexus.Data/App/IRowVersioned.cs)，組態 `IsRowVersion().HasConversion<byte[]>()`；DTO 帶出，PUT 放 body、DELETE 放 `?rowVersion=`。

- `ExpectRowVersion` 比對的是使用者讀到的版本，不是 handler 剛查的。內容沒變也會送 UPDATE：比對一定發生，只改子表也會升主列的版本。
- 新增與修改同一端點（upsert）時，不帶版本是新增、帶版本是修改；對不上回 409，例如 [`SaveRole`](../../backend/src/AiNexus.Features/Admin/AccessControl/Management/SaveRole.cs)。
- 排序、匯入、成員、個人模型政策與容量上限不帶版本：它們的規則靠鎖與整層比對，或是一人一列的整包覆寫。寫入後畫面都會重讀。
- **`Users` 用 `ProfileVersion`**：登入與活動一直在改這張表，rowversion 會讓管理員存不了檔。只有帳號內容的寫入才遞增（`ExpectProfileVersion`，本人改信箱或密碼也算），沒有變更的登入不動；從 1 起算，沒帶版本一定不符。
- **前端**：寫入與寫入後重讀期間停用會帶版本的動作，衝突時草稿作廢並在頁內說明，見 [版本衝突](../frontend/write-conflicts.md)。
- 會影響別筆資料的確認（例如接手會結束別人的期間）帶被影響那筆的版本，使用者確認的才是實際被改的內容。

## 要不要鎖

預設不鎖。依序問：

1. 只改一列？`RowVersion` 就夠。
2. 規則能寫成唯一索引或條件式 UPDATE，撞上時一筆請求回 409 就好？用它們。
3. 規則跨多列又沒有約束可用（例如同一張卡的期間不可重疊），或撞上時輸的是整批寫入（例如新增卡片與匯入）？才以 `AuditTarget.Locking(名稱)` 宣告，範圍取規則涵蓋的最小資源。

- 鎖名集中在模組的 `<Module>Locks`，例如 [`AttendanceLocks`](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceLocks.cs)。多把鎖依名稱排序後才取，不會互相死結。
- 規則要靠鎖才成立、又有不經這裡的寫入者時，對方在交易的第一步也取同一把鎖，例如 [`PunchImporter`](../../backend/src/AiNexus.Features/Office/Attendance/Imports/PunchImporter.cs)。
- 宣告了鎖就要有並行測試，例如 [`CardConcurrencyTests`](../../backend/tests/AiNexus.IntegrationTests/Attendance/CardConcurrencyTests.cs)、[`AdministrativeVersionTests`](../../backend/tests/AiNexus.IntegrationTests/AccessControl/AdministrativeVersionTests.cs)。

現有的鎖：

| 鎖 | 保護的規則 |
| --- | --- |
| [`AccessControlLocks`](../../backend/src/AiNexus.Features/Admin/AccessControl/AccessControlLocks.cs) `Authorization` | 誰持有什麼功能（帳號、角色、群組、功能）；兩位管理員不能互相撤權 |
| `AccessControlLocks.Navigation` | 選單的層數、同頁只出現一次 |
| [`OrganizationLocks`](../../backend/src/AiNexus.Features/Admin/Organization/OrganizationLocks.cs) `Tree` | 單位樹的層數、循環、同層名稱、首長與成員 |
| [`KnowledgeLocks`](../../backend/src/AiNexus.Features/Work/Knowledge/Embeddings/KnowledgeLocks.cs) `EmbeddingProfile` | 只有一個啟用中的向量模型；重建與清除互斥 |
| [`AttendanceLocks`](../../backend/src/AiNexus.Features/Office/Attendance/AttendanceLocks.cs) | 同一張卡的期間、匯入與卡片的關係 |

## 會改變權限的寫入

`AuditTarget.KeepingAccess(actor, 功能)` 宣告：寫入者要持有這個功能，變更後仍要持有。它會取 `Authorization` 鎖，開始時不持有回 403，提交前不再持有回 `Lockout` 並整筆回滾。用於角色、群組、功能與帳號，例如 [`SetUserRoles`](../../backend/src/AiNexus.Features/Admin/Identity/Management/SetUserRoles.cs)。

## 一次處理多筆

`AuditTarget.AuditedPerItem(動作, actor)` 用於匯入這類一批多筆的寫入：每筆各開一次交易、各記一筆稽核。單筆失敗以 `RecordItemFailureAsync` 留失敗稽核後繼續，例如 [`DirectoryProvisioning`](../../backend/src/AiNexus.Features/Admin/Identity/Provisioning/DirectoryProvisioning.cs)。
