# 資料庫建置與權限

給 DBA 與部署人員：以發布套件手動部署 `AiNexus` 的結構與基礎資料、設定 SQL 帳號與讀取隔離。結構變更流程見 [資料庫](../architecture/database.md)，決策見 [ADR 0010](../decisions/0010-database-first-schema.md)。

## 部署

正式機不跑任何部署腳本：開發端以 `Release.ps1` 產生發布包（[ADR 0027](../decisions/0027-single-version-and-tag-based-release.md)），資料庫只需要執行其中一個檔案。新庫以 `scripts/Initialize-Database.ps1` 建立。

```text
<發布包>/database/
├─ update.sql      ← 唯一要執行的檔案（SQLCMD 模式）
├─ changes.md      ← 變更的物件；資料表列出欄位、索引、約束的增減
├─ compare.scmp    ← Visual Studio 結構描述比對，兩份 dacpac 在 compare├─ rollback.sql    ← 程式物件回到起點；資料表用備份回復
└─ manual/         ← 這段期間新增的一次性資料修正（database/App/Scripts/）
```

先停站並完成 [備份](backup.md)，再在 SSMS 開啟 `update.sql`，切到 SQLCMD 模式（查詢 → SQLCMD 模式）連到目標庫執行。資料庫名稱不是 `AiNexus` 時改第一行的 `:setvar DatabaseName`。依序做四件事，任何一步失敗就停止：

1. **前置檢查**：目標庫記錄的資料庫版本必須等於更新包的起點，否則停止並說明，請發版者以 `Release.ps1 -From v<目標庫版本>` 重新產生。上次更新後被手動改過的物件會列在結果並停止；確認可以覆蓋（或已回收進開發庫）後把 `:setvar AcceptDrift` 改成 1 重新執行。
2. **結構**：SqlPackage 比對起點與這一版的 dacpac 產生。可能遺失資料（刪欄位、縮短長度、刪有資料的表）時在該處停止。
3. **全文、種子資料**：可重跑；沒有全文元件時只印「略過」。種子資料帶新功能的頁面、預設授權與選單。
4. **記錄**：寫入版本、這一版應有的結構快照（[ADR 0028](../decisions/0028-schema-drift-and-version-display.md)），並列出與這一版不同的物件（更新前就有的手動修改）。

- 審核用 `changes.md` 與 `compare.scmp`（[結構描述比對](database-verification.md)）；VS 只用來看，不用它產生升級 SQL。
- `manual/` 不在 `update.sql` 內：一次性的資料搬移、修正由工程師判斷是否、何時執行。腳本提交到 `database/App/Scripts/<yyyy-MM-dd>-<描述>.sql` 經 PR 審核，發版時依 git 範圍帶入。
- 更新失敗時以備份回復；只想撤回程式物件（procedure、function、view）時執行 `rollback.sql`，版本紀錄不變，版本頁會把它們列為異動。
- 新庫：`Initialize-Database.ps1` 套用 dacpac、全文與種子資料，並記錄版本與快照；資料庫已存在時拒絕執行，不會動到 DBA 維護的開發庫。`-Version v2026.11.3` 以該版的結構在本機重現客戶的資料庫。
- migration 時代建立的庫若還有 `dbo.__EFMigrationsHistory`，DBA 直接 `DROP TABLE`；程式已不讀取。

## SQL 帳號權限

- 部署帳號（DBA 或發版身分）：結構變更需要 `db_ddladmin`（或 `db_owner`），建新庫需要 `CREATE DATABASE`；只做比對的帳號需要 VIEW DEFINITION。
- runtime 登入另需資料庫層級的 `VIEW DEFINITION`：網站比對結構快照要讀 procedure、view 的定義本文（`releases.udf_SchemaObjects`）；沒有時異動偵測只看得到有權限的物件。
- runtime 登入需要所有業務 schema 的 SELECT／INSERT／UPDATE／DELETE（inline 函式屬 SELECT）與 EXECUTE（procedure），以 schema 授權，新增模組的 procedure 才不必逐支補權限；不給建庫、ALTER 或 `db_owner`。網站啟動時讀 `sys.columns` 比對結構，public 角色預設即可看到有權限物件的欄位。管理與一般端點共用同一條連線。
- 外部系統登入只授權固定物件：公文系統 GD 給用到的表 SELECT 與假日四欄的 UPDATE，見 [出勤與公文系統 GD](../features/attendance-gd.md#連線與帳號權限)。

## 讀取隔離（RCSI）

- 開啟 `READ_COMMITTED_SNAPSHOT` 讓讀取不等寫入，減少阻塞與死結；程式端的併發規則見 [資料庫](../architecture/database.md#讀取隔離rcsi)。
- 代價：更新與刪除的舊版本存在 tempdb version store，長交易會讓它膨脹；每列多 14 bytes。監看 `sys.dm_tran_version_store_space_usage`，並確認 tempdb 有足夠空間。
- 以 `Initialize-Database.ps1` 建立的新庫即開啟；DBA 手動建立的新庫先以 `App.sqlproj` 的設定建庫。既有庫切換需要獨佔、會中斷連線，所以不自動切換；網站啟動時只記 `database.read_committed_snapshot_off` 警告。DBA 在維護時段停站後執行一次：

```sql
ALTER DATABASE [AiNexus] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'AiNexus';
```
