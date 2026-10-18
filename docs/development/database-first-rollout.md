# 資料庫優先導入計畫

把 [ADR 0010](../decisions/0010-database-first-schema.md)～[0014](../decisions/0014-procedure-audit.md) 落地的步驟。專案尚未上線，不保留相容層。**全部完成後刪除這份文件**，長期規則留在 ADR 與 [資料庫](../architecture/database.md)。

## 1. 匯出與 build 工具正式化

- `Export-Database.ps1`：
  - 保留手寫資料夾（`FullText/`、`Seed/`、`Data/`）與 sqlproj。
  - 把全文目錄與全文索引移到 `FullText/`。
  - 排序欄位說明語句，避免只有順序差異的 diff。
  - 支援排除暫存表與備份表（例如 `*_bak`）。
  - 比對資料庫的 collation、相容層級、RCSI 與 sqlproj 設定，不一致時警告。
- `database/App/App.sqlproj` 設定 `Chinese_Taiwan_Stroke_CI_AS`、相容層級 170、RCSI。
- 從開發庫匯出第一版基線並提交；這時欄位說明仍來自 EF `[Comment]`。
- `scripts/tests/Scripts.Tests.ps1` 的入口清單加入新腳本；`Verify.ps1` 加入 `dotnet build database/App`。
- **完成條件**：連續匯出兩次沒有 diff；`Verify.ps1` 通過。

## 2. 資料存取專案與命名

- 建立 `AiNexus.Data`：
  - 從各模組搬入實體與組態，每張表一個檔案。
  - 欄位用到的列舉與值型別一起搬。
  - `NexusDbContext` 改名為 `AppDbContext`。
- 移除 EF 的 `[Comment]`、索引、約束與 `HasData`；組態只留對應需要的部分。
- 拆解 `backend/src/AiNexus.Features/Persistence/`：
  - 通用元件移到 `AiNexus.Platform/Data`。
  - 主資料庫專屬的元件移到 `AiNexus.Data/App`。
  - 刪除 `Migrations/`、`ReleaseScripts/`、`SqliteModel.cs`、`DesignTimeDbContextFactory.cs`、`MigrationBuilderExtensions.cs`。
- 型別改名：`NexusUser`、`NexusProblemWriter`、`NexusConfiguration`、`NexusConfigResolver`、`NexusMasterDatabase`。
- 移除冗餘：搬移後沒有呼叫端的型別與方法、只為 SQLite 或 migration 存在的分支、重複的組態與轉換一併刪除；多次查詢再在記憶體組合的 LINQ，能用 `LeftJoin` 改成一句就改。
- 架構測試：
  - 改寫 `backend/tests/AiNexus.ArchitectureTests/DatabaseModelTests.cs`，檢查 schema 與資料夾一致，並禁止 Features 定義資料表。
  - 確認 `ModuleBoundaryTests` 仍成立。
- **完成條件**：`dotnet build` 0 warning；EF 模型與基線 dacpac 比對一致。

## 3. 測試改用 SQL Server

- 測試啟動時，把 dacpac 部署到 LocalDB 建出範本資料庫，每個 host 從範本複製出自己的資料庫。PoC 的 `poc/database/EfSchemaCheck` 改寫成正式測試。
- `backend/tests/AiNexus.IntegrationTests/Support/NexusFactory.cs` 改連 LocalDB。全文元件不存在時，相關測試標為略過並註明原因。
- 每支被程式呼叫的 procedure 至少一個整合測試。
- 新 ADR 取代 [ADR 0005](../decisions/0005-sqlite-tests-sqlserver-opt-in.md)，並寫明 LocalDB 無法啟動時的排除步驟。
- **風險**：
  - LocalDB 偶爾無法啟動（PoC 期間遇過一次），要在 [測試](testing.md) 寫明排除步驟。
  - 整體測試時間會變長，需要實測。如果超過可接受範圍，改成每個測試類別共用資料庫，並在每個測試結束時回復交易。

## 4. 種子資料與發版

- `HasData` 與 `FeatureSeed` 的基礎資料改寫成 `database/App/Seed/*.sql`：用 `MERGE`，可重複執行。
- 新增部署腳本（例如 `Deploy-Database.ps1`），依序執行：
  1. `Data/<版本>/pre-*.sql`
  2. DeployReport 與 Script（交給 DBA 審核），或直接 Publish（僅限開發／測試環境）
  3. `FullText/`（偵測不到全文元件時跳過）
  4. `Data/<版本>/post-*.sql`
  5. `Seed/`
- `scripts/Publish-IIS.ps1` 的發布套件改帶 dacpac 與上述腳本，不再產生 `migrations.sql`。
- 啟動檢查：`DatabaseSchema` 改為「EF 模型 vs `sys.columns`」的唯讀比對；`Persistence:ApplyMigrationsOnStartup` 刪除。
- 移除 `__EFMigrationsHistory`，調整執行期帳號的權限。
- 漂移偵測：以已發版 dacpac 對指定環境跑 DeployReport，輸出報告。
- **完成條件**：從空庫可完整建出可用的系統；對舊版本的庫可產生升級 SQL；漂移報告能抓到模擬的熱修。

## 5. procedure 範本與稽核

- `ISqlDatabase<T>` 加入呼叫 procedure 的方法（`CommandType.StoredProcedure`）；寫入用的方法依 [ADR 0014](../decisions/0014-procedure-audit.md) 在同一個批次設定稽核情境。
- 在開發庫建立 `audit.udf_AuditContext` 與 `audit.usp_WriteAuditEvent`，匯出後提交；整合測試的稽核 helper 一併完成。
- 兩支範例，讓後續開發者照抄：
  - 讀取：`ListAdminUsers` 改為 `administration.usp_ListAdminUsers`，handler 只負責驗證與呼叫。
  - 寫入：挑一個不需要前後快照的單純管理寫入改成 procedure，示範交易寫法、成功稽核由 procedure 寫、失敗稽核由程式寫、成功後發 domain event。
- 流程：在 SSMS 建立 → 匯出 → 程式呼叫 → 整合測試。

## 6. fork 範本

- 新增 `.template.config/template.json`（`sourceName: AiNexus`），並實際 fork 出一個新名稱的專案，從 build、建庫到啟動完整驗證一次。
- PowerShell 模組函式的 `-Nexus` 改為中性名稱。

## 7. 文件

依 ADR 改寫以下文件，並刪除過時段落：
- [資料庫](../architecture/database.md)、[後端慣例](../architecture/backend-conventions.md)、[測試](testing.md)
- [資料庫建置與權限](../operations/database-setup.md)、[結構驗證](../operations/database-verification.md)、[IIS 部署](../operations/iis-deployment.md)
- `CLAUDE.md` 的指令與「不手改 migrations」規則

## 順序

1 → 2 → 3 → 4 依序進行，各一個 PR；5、6 在 4 之後可以平行；7 隨各階段一起更新。
