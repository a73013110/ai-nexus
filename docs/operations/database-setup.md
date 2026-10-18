# 資料庫建置與權限

給 DBA 與部署人員：建立 `AiNexus`、部署結構、設定 SQL 帳號與讀取隔離。資料表與結構變更流程見 [資料庫](../architecture/database.md)。

## 初始化

```powershell
./scripts/Initialize-Database.ps1
```

- 只在 `AiNexus` 不存在時建立空庫並開啟 RCSI；不會 DROP 或清空資料，也不建立資料表。DBA 也可以自建空庫（記得開啟 RCSI，見下節）。
- 結構以發布套件的 `database\App.dacpac` 部署：DBA 以 SqlPackage `/Action:Script` 產生升級 SQL 審閱後執行（開發／測試環境可直接 `/Action:Publish`），有全文元件時再執行 `database/App/FullText/` 的目錄與索引腳本，最後執行 `database/App/Seed/`；完成後以 `Verify-Database.ps1` [驗證實際結構](database-verification.md)。整合成一支部署腳本是[導入計畫](../development/database-first-rollout.md)第 4 節的工作。

## SQL 帳號權限

- 正式環境的 DDL 用獨立部署帳號。
- runtime 登入需要所有業務 schema 的 SELECT／INSERT／UPDATE／DELETE，不給建庫、ALTER 或 `db_owner`。管理與一般端點共用同一條連線。
- 外部來源登入只授權固定 view 的 SELECT（見 [INTEGRATIONS](../features/integrations.md)）。

## 讀取隔離（RCSI）

- 開啟 `READ_COMMITTED_SNAPSHOT` 讓讀取不等寫入，減少阻塞與死結；程式端的併發規則見 [資料庫](../architecture/database.md#讀取隔離rcsi)。
- 代價：更新與刪除的舊版本存在 tempdb version store，長交易會讓它膨脹；每列多 14 bytes。監看 `sys.dm_tran_version_store_space_usage`，並確認 tempdb 有足夠空間。
- `db init` 建庫後即開啟。既有庫切換需要獨佔、會中斷連線，所以不自動切換；網站啟動時只記 `database.read_committed_snapshot_off` 警告。DBA 在維護時段停站後執行一次：

```sql
ALTER DATABASE [AiNexus] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'AiNexus';
```
