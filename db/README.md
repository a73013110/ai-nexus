# SQL migration 交付

schema、migration 規則與 SQL 權限見 [docs/DATABASE](../docs/DATABASE.md)，SQL 帳密與 TLS 設定見 [docs/CONFIGURATION](../docs/CONFIGURATION.md)。

- [migrations.sql](migrations.sql)：由 EF 產生的 idempotent SQL，依 `__EFMigrationsHistory` 只執行未套用的 migration。DBA 先建立空的 AiNexus，選擇該資料庫後審閱執行；其中沒有 CREATE LOGIN／DATABASE 或秘密。
- [integrations/](integrations/)：外部來源資料庫的授權 view 範本，由該來源的 DBA 執行，不屬於 AiNexus 資料庫。

本機或有 DDL 權限的維運身分可直接執行：

```powershell
./scripts/Initialize-Database.ps1
```

產生 SQL（migration 有變動時與 migration 一起提交）：

```powershell
dotnet ef migrations script --idempotent --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Host --output db/migrations.sql
```
