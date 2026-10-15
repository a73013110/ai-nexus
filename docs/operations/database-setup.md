# 資料庫建置與權限

給 DBA 與部署人員：建立 `AiNexus`、套用 schema、設定 SQL 帳號與讀取隔離。資料表與 migration 規則見 [資料庫](../architecture/database.md)。

## 初始化

```powershell
./scripts/Initialize-Database.ps1
```

- 只在 `AiNexus` 不存在時建庫（並開啟 RCSI），然後套用未完成的 migration；不會 DROP、清空資料或修改 history。
- DBA 也可以先自建空的 `AiNexus`（記得開啟 RCSI，見下節），再審閱執行發布套件的 `migrations.sql`（idempotent，只執行未套用的部分），其中沒有 CREATE LOGIN／DATABASE 或秘密。
- **2026-10-09 以前建立的資料庫無法升級**（舊 migration 已刪除，不保留升級路徑），必須刪除後重新初始化；初始化遇到舊表會直接失敗。

## SQL 帳號權限

- 正式環境的 DDL 用獨立部署帳號。
- runtime 登入需要所有業務 schema 的 SELECT／INSERT／UPDATE／DELETE 與 `dbo.__EFMigrationsHistory` 的 SELECT，不給建庫、ALTER 或 `db_owner`。管理與一般端點共用同一條連線。
- 外部來源登入只授權固定 view 的 SELECT（見 [INTEGRATIONS](../features/integrations.md)）。

## 讀取隔離（RCSI）

- 開啟 `READ_COMMITTED_SNAPSHOT` 讓讀取不等寫入，減少阻塞與死結；程式端的併發規則見 [資料庫](../architecture/database.md#讀取隔離rcsi)。
- 代價：更新與刪除的舊版本存在 tempdb version store，長交易會讓它膨脹；每列多 14 bytes。監看 `sys.dm_tran_version_store_space_usage`，並確認 tempdb 有足夠空間。
- `db init` 建庫後即開啟。既有庫切換需要獨佔、會中斷連線，所以不自動切換也不用 migration；網站啟動時只記 `database.read_committed_snapshot_off` 警告。DBA 在維護時段停站後執行一次：

```sql
ALTER DATABASE [AiNexus] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'AiNexus';
```
