# 資料庫優先導入計畫

把 [ADR 0010](../decisions/0010-database-first-schema.md)～[0014](../decisions/0014-procedure-audit.md) 落地的步驟。專案尚未上線，不保留相容層。**全部完成後刪除這份文件**，長期規則留在 ADR 與 [資料庫](../architecture/database.md)。

## 3. 測試改用 SQL Server

- 測試啟動時，把 dacpac 部署到 LocalDB 建出範本資料庫，每個 host 從範本複製出自己的資料庫。PoC 的 `poc/database/EfSchemaCheck` 改寫成正式測試。
- `backend/tests/AiNexus.IntegrationTests/Support/NexusFactory.cs` 改連 LocalDB。全文元件不存在時，相關測試標為略過並註明原因。
- 每支被程式呼叫的 procedure 至少一個整合測試。
- 新 ADR 取代 [ADR 0005](../decisions/0005-sqlite-tests-sqlserver-opt-in.md)，並寫明 LocalDB 無法啟動時的排除步驟。
- 階段 2 留下的 SQLite 過渡碼一併刪除：
  - 測試端的 `Support/SqliteModel.cs`（向量、時間排序的模型差異）與 `Support/BaselineSeed.cs`（改為執行 `database/App/Seed/`）。
  - 正式程式只為 SQLite 存在的分支：`InMemoryRetrievalStore` 與 `KnowledgeModule` 的選擇、`DocumentIndexer` 的 `SearchId` 自編號、`EmbeddingVectorStore`、`SpendReports`、`UsageReports`、`DiagnosticStore`、`ReadRetrievalCapabilities`、Platform 的 `DashboardDays` 中 `IsSqlServer()` 的另一支。
  - `Support/SqlServerDatabase.cs` 目前每個測試以 SqlPackage 部署一次 dacpac，改用範本資料庫。
- EF 組態已不宣告索引與約束，SQLite 測試因此不再驗證唯一性與 filtered index（例如每人一個進行中的生成）；改用 LocalDB 後補上對應的整合測試。
- `DatabaseModelTests` 原本檢查每個表與欄位都有說明；說明改由 DBA 維護，改成對 LocalDB 檢查 `MS_Description`（[ADR 0012](../decisions/0012-database-naming-and-foreign-keys.md)）。
- **風險**：
  - LocalDB 偶爾無法啟動（PoC 期間遇過一次），要在 [測試](testing.md) 寫明排除步驟。
  - 整體測試時間會變長，需要實測。如果超過可接受範圍，改成每個測試類別共用資料庫，並在每個測試結束時回復交易。

## 4. 種子資料與發版

- 基礎資料：`HasData`／`FeatureSeed` 已在階段 2 移除，內容轉寫成 `database/App/Seed/accesscontrol.sql`（只新增缺少的列，可重複執行，已在 LocalDB 連跑兩次驗證）；審核後納入部署腳本。
- 新增部署腳本（例如 `Deploy-Database.ps1`），依序執行：
  1. `Data/<版本>/pre-*.sql`
  2. DeployReport 與 Script（交給 DBA 審核），或直接 Publish（僅限開發／測試環境）
  3. `FullText/`：先 `Catalogs/` 再 `Indexes/`，偵測不到全文元件時跳過；匯出的語句沒有存在檢查，要能重複執行
  4. `Data/<版本>/post-*.sql`
  5. `Seed/`
- `scripts/Publish-IIS.ps1` 已在階段 2 改帶 `database/App.dacpac` 與 `Verify-Database.ps1`（`migrations.sql`、拆檔 SQL、`db script`／`db diff` 已刪除）；再加入上述部署腳本與 `FullText/`、`Seed/`。
- 主機指令 `db init`（`DatabaseInitializer`）目前只建立空庫並開啟 RCSI；部署腳本以 dacpac 建庫後（sqlproj 已設定 RCSI）評估是否刪除。
- 啟動檢查：`DatabaseSchema` 目前只檢查 RCSI（migration 檢查與 `Persistence:ApplyMigrationsOnStartup` 已在階段 2 刪除），加入「EF 模型 vs `sys.columns`」的唯讀比對，可由 `poc/database/EfSchemaCheck` 改寫。
- 移除既有環境的 `__EFMigrationsHistory`（執行期已不讀取，權限說明已更新）。
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
- 設定鍵 `Databases:Nexus`（`AppDbContext.DatabaseName`）仍帶產品名，改為角色名 `App`，連同範本設定、腳本與文件。

## 7. 文件

階段 2 已把 migration 相關的段落改為現況；後續階段再依 ADR 改寫，並刪除過時段落：
- [測試](testing.md)（階段 3）
- [資料庫建置與權限](../operations/database-setup.md)、[結構驗證](../operations/database-verification.md)、[IIS 部署](../operations/iis-deployment.md)（階段 4 的部署腳本）

## 順序

3 → 4 依序進行，各一個 PR；5、6 在 4 之後可以平行；7 隨各階段一起更新。
