# 資料庫優先導入計畫

把 [ADR 0010](../decisions/0010-database-first-schema.md)～[0014](../decisions/0014-procedure-audit.md) 落地的步驟。專案尚未上線，不保留相容層。**全部完成後刪除這份文件**，長期規則留在 ADR 與 [資料庫](../architecture/database.md)。

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
- 啟動檢查：`DatabaseSchema` 目前只檢查 RCSI（migration 檢查與 `Persistence:ApplyMigrationsOnStartup` 已在階段 2 刪除），加入「EF 模型 vs `sys.columns`」的唯讀比對：呼叫階段 3 的 `ModelSchema.CompareAsync`（`backend/src/AiNexus.Platform/Data/Sql/ModelSchema.cs`），`Blocking` 的差異停止啟動，其餘只警告。
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
- [資料庫建置與權限](../operations/database-setup.md)、[結構驗證](../operations/database-verification.md)、[IIS 部署](../operations/iis-deployment.md)（階段 4 的部署腳本）

## 順序

4 先做；5、6 在 4 之後可以平行；7 隨各階段一起更新。
