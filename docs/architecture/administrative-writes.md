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

取得行政寫入鎖，開 Serializable 交易，檢查權限並讀前值，執行變更並 `SaveChanges`，再檢查一次權限並讀後值，最後提交。

- 操作者變更後失去該功能（例如撤掉自己的角色）時回滾並回 lockout。
- 這把鎖由所有模組共用，同時只有一筆寫入能進行：耗時的操作放在 mutation 外面，使用者端的高頻寫入不用它。

## 怎麼寫

```csharp
write.ExecuteAsync(actor, CardRules.Target(db, id), async () =>
{
    // 只放業務邏輯；回傳錯誤就回滾，不必自己 SaveChanges。
}, ct);
```

- **寫入對象 `AuditTarget`**：功能、資源 ID、快照怎麼讀。同一資源有多個 slice 時放在 `<Resource>Rules.Target`，稽核形狀才不會各寫各的；只有一個 slice 用時直接寫在 handler。
- **快照**：只投影要留證的欄位，鍵名必須在 [`AuditRedactor`](../../backend/src/AiNexus.Platform/Auditing/AuditRedactor.cs) 白名單內。照常寫 `AsNoTracking`；流程也會以不追蹤讀取，漏寫時不會記到舊值。不要用 `FindAsync`：它先查追蹤中的實體，不受這個設定影響。
- **mutation 的回傳值**：這時還沒寫入，不可依賴資料庫才產生的值；需要時用無結果的版本，提交後再查。
