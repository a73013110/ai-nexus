# SQL migration 交付

物件／schema／索引／關聯及權限說明在 [docs/DATABASE](../docs/DATABASE.md)，SQL 帳密與 TLS 設定在 [docs/CONFIGURATION](../docs/CONFIGURATION.md)。

本次增量版本 `20261007040053_SystemDiagnostics` 新增日誌表、查證代碼／持久流程欄位、索引與三項管理日誌 feature grants。SQL 查詢儲存與業務 transaction 隔離；站外 journal 的去重補送、保留、稽核政策及部署順序見 [DIAGNOSTICS](../docs/DIAGNOSTICS.md)。

```powershell
./scripts/Initialize-Database.ps1
```

此本機工具只建立／更新 AiNexus，使用已設定登入的建庫／DDL 權限。InitialCreate 建立空資料庫基線，後續 migrations 延續此基線升級；升級前停止舊 host。初始化不刪庫、不清空或重寫 history；其他基線會拒絕。

[Migration SQL](migrations.sql) 包含 InitialCreate 與後續增量版本，依 \_\_EFMigrationsHistory 以 idempotent 方式執行；DBA 先建立 AiNexus，選擇該資料庫後審閱執行。正式由獨立管理登入部署 schema，應用登入只需必要 DML，預設不在 startup 自動 migration。已設定 SQL 的 host 會先唯讀檢查版本，若有未套用 migration 則停止啟動並列出版本。

產生 SQL：

```powershell
dotnet ef migrations script --idempotent --project backend/src/AiNexus.Host --output db/migrations.sql
```

不要手改 migration history、把帳密放 SQL、或在正式資料上以 EnsureCreated 取代 migrations。來源 migration／designer／snapshot 位於 backend/src/AiNexus.Host/BuildingBlocks/Migrations。

初始化與 SQL Server startup 也驗證模型與 snapshot 一致；可另執行 `dotnet ef migrations has-pending-model-changes --project backend/src/AiNexus.Host`。原檔不在 DB，完整備份需同一時點的 SQL＋站外附件目錄，見 [BACKUP](../docs/BACKUP.md)。
