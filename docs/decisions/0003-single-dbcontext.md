# 0003 單一 DbContext

- **狀態**：採用（2026-10）
- **決定**：所有模組共用一個 `NexusDbContext` 與一個 SQL Server 資料庫；每個模組一個 schema（名稱＝模組資料夾小寫）。不拆多個 DbContext 或多個資料庫。

## 原因

- 跨模組外鍵（例如訊息連附件、資源連專案）與「一個請求一個交易」是需求：刪除對話要同時撤銷分享，建立 run 要同時預約配額。
- domain event 在 `SaveChangesAsync` 內同交易分派（見 [模組邊界](../architecture/module-boundaries.md#直接呼叫或-domain-event)），多個 DbContext 會失去這個保證，得改用 outbox 或分散式交易。
- 模組的資料歸屬改由 schema 與測試表達：`DatabaseModelTests` 檢查每張表的 schema 與擁有它的模組一致，跨模組外鍵集中在 `Persistence/CrossModuleRelationships.cs`。

## 何時重新評估

某個模組要拆成獨立服務時（見 [ADR 0002](0002-vertical-slice-single-features-project.md)）；那時它的跨模組外鍵要改成識別碼參照，同交易的副作用要改成 outbox。
