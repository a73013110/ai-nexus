# SQL migration 交付

物件／schema／索引／關聯及權限說明在 [docs/DATABASE](../docs/DATABASE.md)，SQL 帳密與 TLS 設定在 [docs/CONFIGURATION](../docs/CONFIGURATION.md)。

```powershell
./scripts/Initialize-Database.ps1
```

此本機工具只建立／更新 AiNexus，使用已設定登入的建庫／DDL 權限。已有資料庫只套用新 migrations，沒有重建或清空資料。

[Migration SQL](migrations.sql) 包含所有已提交 migrations，依 __EFMigrationsHistory 以 idempotent 方式執行；DBA 先建立 AiNexus，選擇該資料庫後審閱執行。正式由獨立管理登入部署 schema，應用登入只需必要 DML，預設不在 startup 自動 migration。已設定 SQL 的 host 會先唯讀檢查版本，若有未套用 migration 則停止啟動並列出版本。

產生 SQL：

```powershell
dotnet ef migrations script --idempotent --project backend/src/AiNexus.Api --output db/migrations.sql
```

不要手改 migration history、把帳密放 SQL、或在正式資料上以 EnsureCreated 取代 migrations。來源 migration／designer／snapshot 位於 backend/src/AiNexus.Api/BuildingBlocks/Migrations。
