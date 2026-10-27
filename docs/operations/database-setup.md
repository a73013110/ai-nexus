# 資料庫建置與權限

給 DBA 與部署人員：以發布套件手動部署 `AiNexus` 的結構與基礎資料、設定 SQL 帳號與讀取隔離。結構變更流程見 [資料庫](../architecture/database.md)，決策見 [ADR 0010](../decisions/0010-database-first-schema.md)。

## 部署

正式機不跑任何部署腳本：結構用 Visual Studio 結構描述比對產生升級 SQL，手寫的 SQL 由 `Publish-IIS.ps1` 依執行順序編號放進套件。新庫與升級是同一套步驟。

```text
<套件>/database/
├─ App.dacpac      ← 這一版應有的結構，與 app 出自同一份原始碼
└─ release/
   ├─ README.txt                       ← 這一版的步驟清單，執行順序以它為準
   ├─ 1-01-pre-*.sql                   ← Data/<版本>/pre-*.sql，結構前的一次性資料修正
   ├─ 3-01-fulltext-KnowledgeSearch.sql
   ├─ 3-02-fulltext-knowledge.Chunks.sql
   ├─ 4-01-post-*.sql                  ← Data/<版本>/post-*.sql，結構後的一次性資料修正
   └─ 5-01-seed-accesscontrol.sql      ← 功能、授權、選單的種子資料
```

先停站並完成 [備份](backup.md)，再在 SSMS 連到目標庫依序執行：

1. `1-*.sql`（這一版有才會出現）。
2. **結構**：[結構描述比對](database-verification.md)，來源 `App.dacpac`、目標正式庫，審核差異後產生指令碼並執行（產生的指令碼含 `:setvar`，要開 SQLCMD 模式）。
3. `3-*.sql` 全文：沒有全文元件或物件已存在時只印「略過」，可重跑；已存在物件的定義變更由 DBA 手動處理。
4. `4-*.sql`（這一版有才會出現）。
5. `5-*.sql` 種子資料：只新增缺少的列、可重跑。**不能省略**：新功能的頁面、預設授權與選單都在這裡，漏跑時新頁面不會出現在選單，也無法授權。
6. 再比對一次，應該沒有差異，見 [結構驗證](database-verification.md#部署後驗證)。

- `release\` 的檔案用一般模式執行即可（只有 T-SQL 與 GO）；`Publish-IIS.ps1` 會拒絕含 SQLCMD 指令的手寫腳本。
- 一次性資料修正要在產生套件時加 `-Release <版本>` 才會帶入，對應 `database/App/Data/<版本>/`；pre 腳本只搬移資料，不改結構。
- 可能遺失資料（刪欄位、縮短長度）時比對會擋下；確定要刪的那次先以 pre 腳本搬資料，再由 DBA 審核後放行。
- 新庫：比對目標選一個空的資料庫即可；collation、相容層級與 RCSI 的設定見 [讀取隔離](#讀取隔離rcsi) 與 `database/App/App.sqlproj`。開發機建新庫用 `scripts/Initialize-Database.ps1`，資料庫已存在時拒絕執行，不會動到 DBA 維護的開發庫。
- migration 時代建立的庫若還有 `dbo.__EFMigrationsHistory`，DBA 直接 `DROP TABLE`；程式已不讀取。

## SQL 帳號權限

- 部署帳號（DBA 或發版身分）：結構變更需要 `db_ddladmin`（或 `db_owner`），建新庫需要 `CREATE DATABASE`；只做比對的帳號需要 VIEW DEFINITION。
- runtime 登入需要所有業務 schema 的 SELECT／INSERT／UPDATE／DELETE（inline 函式屬 SELECT）與 EXECUTE（procedure），以 schema 授權，新增模組的 procedure 才不必逐支補權限；不給建庫、ALTER 或 `db_owner`。網站啟動時讀 `sys.columns` 比對結構，public 角色預設即可看到有權限物件的欄位。管理與一般端點共用同一條連線。
- 外部系統登入只授權固定物件：公文系統 GD 給用到的表 SELECT 與假日四欄的 UPDATE，見 [出勤與公文系統 GD](../features/attendance-gd.md#連線與帳號權限)。

## 讀取隔離（RCSI）

- 開啟 `READ_COMMITTED_SNAPSHOT` 讓讀取不等寫入，減少阻塞與死結；程式端的併發規則見 [資料庫](../architecture/database.md#讀取隔離rcsi)。
- 代價：更新與刪除的舊版本存在 tempdb version store，長交易會讓它膨脹；每列多 14 bytes。監看 `sys.dm_tran_version_store_space_usage`，並確認 tempdb 有足夠空間。
- 以 `Initialize-Database.ps1` 建立的新庫即開啟；以結構描述比對建立的新庫，DBA 先以 `App.sqlproj` 的設定建庫。既有庫切換需要獨佔、會中斷連線，所以不自動切換；網站啟動時只記 `database.read_committed_snapshot_off` 警告。DBA 在維護時段停站後執行一次：

```sql
ALTER DATABASE [AiNexus] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'AiNexus';
```
