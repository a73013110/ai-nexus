# 資料庫建置與權限

給 DBA 與部署人員：以發布套件部署 `AiNexus` 的結構與基礎資料、設定 SQL 帳號與讀取隔離。結構變更流程見 [資料庫](../architecture/database.md)，決策見 [ADR 0010](../decisions/0010-database-first-schema.md)。

## 部署

發布套件的 `Deploy-Database.ps1` 依序執行 `database\Data\<版本>\pre-*.sql` → 結構（`App.dacpac`）→ `FullText\` → `Data\<版本>\post-*.sql` → `Seed\`。新庫與升級是同一套步驟；目標庫不存在時，升級 SQL 會建立它並套用 collation、相容層級與 RCSI。

```powershell
Set-Location 'D:\Packages\AiNexus'
./Deploy-Database.ps1 -Server 'sql.company.internal' -Release 2026.11.0                  # 1. 產生報告與升級 SQL（唯讀）
./Deploy-Database.ps1 -Server 'sql.company.internal' -Release 2026.11.0 -Action Pre      # 2. 一次性資料修正（結構前）
#                                                                                        # 3. DBA 審核後以 SSMS 的 SQLCMD 模式執行 Upgrade.sql
./Deploy-Database.ps1 -Server 'sql.company.internal' -Release 2026.11.0 -Action Post     # 4. 全文、post 腳本、種子資料
./Verify-Database.ps1 -Server 'sql.company.internal' -DacpacPath .\database\App.dacpac  # 5. 比對實際結構
```

- 第 1 步寫出 `<資料庫>-<時間>\DeployReport.xml`（差異清單）與 `Upgrade.sql`，並列出 Pre、Post 會執行的腳本；不改資料庫。所有環境都用同一個 dacpac 比對。
- `-Release` 對應 `database\Data\<版本>\`；這一版沒有一次性修正時可省略。pre 腳本只搬移資料，不改結構（升級 SQL 在 pre 之前產生）。
- 發版參數：可能遺失資料（刪欄位、縮短長度）時整份中止；確定要刪的那次先以 pre 腳本搬資料，或審核後單次加 `-AllowDataLoss`。不刪除目標多出的物件、不自動給 NOT NULL 新欄位預設值；忽略權限、使用者、角色、檔案群組與欄位順序；既有庫不改資料庫層級選項。
- 全文：偵測不到全文元件時跳過（混合檢索改用 vector）；目錄與索引已存在時略過整檔，定義變更由 DBA 手動處理。
- 種子資料只新增缺少的列，可重複執行；要改既有環境的值，寫在 `Data\<版本>\post-*.sql`。
- 開發機與測試環境可用 `-Action Publish` 一次做完 1～4（直接套用結構，不經審核）；本機建新庫用 `scripts/Initialize-Database.ps1`，資料庫已存在時拒絕執行，不會動到 DBA 維護的開發庫。
- 需要 SqlPackage，找法與安裝方式同 [部署後結構驗證](database-verification.md#安裝-sqlpackage)。
- migration 時代建立的庫若還有 `dbo.__EFMigrationsHistory`，DBA 直接 `DROP TABLE`；程式已不讀取，`Verify-Database.ps1` 會把它列為多出的物件。

## SQL 帳號權限

- 部署帳號（DBA 或發版身分）：結構變更需要 `db_ddladmin`（或 `db_owner`），建新庫需要 `CREATE DATABASE`；`Verify-Database.ps1` 只需要 VIEW DEFINITION。
- runtime 登入需要所有業務 schema 的 SELECT／INSERT／UPDATE／DELETE（inline 函式屬 SELECT）與 EXECUTE（procedure），以 schema 授權，新增模組的 procedure 才不必逐支補權限；不給建庫、ALTER 或 `db_owner`。網站啟動時讀 `sys.columns` 比對結構，public 角色預設即可看到有權限物件的欄位。管理與一般端點共用同一條連線。
- 外部來源登入只授權固定物件：校務只給 SELECT，見 [INTEGRATIONS](../features/integrations.md)；公文系統 GD 另加假日四欄的 UPDATE，見 [出勤與公文系統 GD](../features/attendance-gd.md#連線與帳號權限)。

## 讀取隔離（RCSI）

- 開啟 `READ_COMMITTED_SNAPSHOT` 讓讀取不等寫入，減少阻塞與死結；程式端的併發規則見 [資料庫](../architecture/database.md#讀取隔離rcsi)。
- 代價：更新與刪除的舊版本存在 tempdb version store，長交易會讓它膨脹；每列多 14 bytes。監看 `sys.dm_tran_version_store_space_usage`，並確認 tempdb 有足夠空間。
- 以部署腳本建立的新庫即開啟。既有庫切換需要獨佔、會中斷連線，所以不自動切換；網站啟動時只記 `database.read_committed_snapshot_off` 警告。DBA 在維護時段停站後執行一次：

```sql
ALTER DATABASE [AiNexus] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'AiNexus';
```
