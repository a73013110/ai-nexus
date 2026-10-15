# 資料庫

- 業務資料在 SQL Server 2025 的 `AiNexus` 資料庫，由單一 `NexusDbContext` 對應；跨模組外鍵與同一交易可直接使用。
- **每個模組一個 schema**，名稱＝模組資料夾名小寫。`DatabaseModelTests` 強制這條規則；唯一例外是 `Persistence/AuditEvent.cs`（`NexusDbContext` 為所有模組寫入並遮罩稽核紀錄），它的表在 `audit`。
- **說明跟著實體走**：資料表與欄位說明寫在實體類別與屬性上的 `[Comment]`（shadow 屬性用 `HasComment`），EF 會產生 SQL Server 的 `MS_Description`。漏寫時 `DatabaseModelTests` 失敗。索引、約束與 schema 不另寫說明。
- **migration 基線**：2026-10-09 重建為單一 `InitialCreate`，之後一律追加具名 migration，不再重置基線。

## Schema 與模組

`accesscontrol`、`administration`、`artifacts`、`attachments`、`audit`、`billing`、`collaboration`、`conversations`、`diagnostics`、`identity`、`inference`、`integrations`、`jobs`、`knowledge`、`library`、`notifications`、`projects`、`quality`、`repositories`、`sharing`、`websearch`；`dbo` 只有 `__EFMigrationsHistory`。

欄位、型別與說明以實體及其 `IEntityTypeConfiguration` 為準；兩端分屬不同模組的外鍵集中在 `Persistence/CrossModuleRelationships.cs`。

## 欄位規則

- 能界定長度的字串一律 `HasMaxLength`，上限最好與 validator 共用常數（例如 `PromptTemplate.ContentMaxLength`）；超過 4000 字元的上限在 SQL Server 會是 `nvarchar(max)`，但上限仍由程式檢查。
- 時間用 `DateTimeOffset`（UTC）；金額 `decimal(20,8)`，未知費用保持 null。
- 原始附件不進資料庫，只保存站外儲存識別與 metadata（見 [附件保存](../features/attachment-storage.md)）。
- 只有 `Conversation`、`WorkspaceResource` 套用具名 query filter `SoftDelete`；其他刪除是實體刪除。

## 授權關聯

使用者→角色→群組→功能見 [身分與授權](access-control.md)；資源（專案、知識庫、成果）共用 `collaboration` 的 `Resources`／`ResourceMembers`／`ResourceGroups` ACL。功能 grant 不等於資料 grant：knowledge 不授予全庫閱讀，專案不公開彼此的私人對話。

## 重要約束與索引（為什麼存在）

- `GenerationRuns`：`OwnerId`＋`IdempotencyKey` 唯一索引防重複送出；`ActiveOwnerId` 非空的 filtered unique index 限制每人一個進行中的生成；`ActiveOwnerId`＋`LeaseExpiresAt` 索引供租約復原。它與 `ModelInvocations`的 `CreatedAt` 覆蓋索引供全平台用量報表依日期讀取。
- `RunEvents.CreatedAt` 供保留清理分批刪除（每批自動提交，避免鎖升級與長交易）；`Resources` 的 `Kind`＋`UpdatedAt`（INCLUDE `OwnerId`、`ParentId`、`IsDeleted`）供 `ResourceAccess` 依種類列出可讀資源。
- `Messages.ParentId` 形成訊息樹，`Conversations.ActiveLeafId` 決定目前分支；編輯與重新生成都新增訊息，不覆寫。
- `ArtifactRevisions` 以 `ArtifactId`＋`Version` 為主鍵；expected version 不符時不覆蓋他人的修改。
- `BackgroundJobs.ActiveKey` 唯一，避免同一業務重複排程；`LeaseToken` 做 fencing，過期 worker 不能提交。
- `EvaluationResults` 以 `RunId`＋`CaseIndex`＋`VariantIndex` 為主鍵，重試時跳過已完成的結果。
- `AuditEvents` 以 `At` 及 `Action`／`ResourceId`／`ActorId`＋`Id` 索引支援日期篩選與遞減游標；`DiagnosticEvents` 以 `At`＋`LogId` 為叢集索引，`LogId` 唯一，補送時去重。
- 重要業務外鍵採 `Restrict`，刪使用者或專案不會連鎖刪除歷史；純附屬資料才 `Cascade`。
- `knowledge.Chunks` 有 `KnowledgeSearch` 全文索引（繁中 1028 斷詞）。`CREATE FULLTEXT` 不能在交易內執行，所以由 `InitialCreate` 末尾的 raw SQL 建立；缺全文元件或斷詞器時跳過並警示，執行期改用 vector 模式。向量表與擴充方式見 [VECTOR_ARCHITECTURE](../research/vector-architecture.md)。

## 讀取隔離（RCSI）

資料庫開啟 `READ_COMMITTED_SNAPSHOT`：一般讀取取最後已提交版本，不等寫入。所以防併發只能靠會加鎖的做法：`Serializable`、`UPDLOCK`／`HOLDLOCK`、唯一索引、條件式 `ExecuteUpdate`，或先以無害 UPDATE 鎖擁有者列再查詢（查詢開始時才取快照）。既有庫的切換步驟與代價見 [資料庫建置與權限](../operations/database-setup.md)。

## Migration

```powershell
dotnet ef migrations add DescriptiveChange --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Host --output-dir Persistence/Migrations
```

- 先改實體與組態（含 `[Comment]`），再產生 migration；一起提交 source、designer 與 snapshot。給 DBA 的 SQL 不進版控，由 `Publish-IIS.ps1` 發布時產生（`migrations.sql`；加 `-FromMigration` 另有[拆檔的 `database/`](../operations/iis-deployment.md#給-dba-的拆檔-sql)）。不要手改產生的 migration，只有 EF 表達不了的 DDL（例如全文索引）才在產生後加入 `migrationBuilder.Sql`。
- **改資料的手寫 SQL 必須用 `migrationBuilder.DataSql(sql, "schema.Table")`**：拆檔只把它與 `HasData` 放進 `data/`，`Sql` 一律算結構。
- 不可有在 SQL Server 重建資料表的操作（既有表的叢集鍵／索引、IDENTITY 等），拆檔會失敗並列出原因。
- 已設定 SQL 的 host 在 HTTP 與背景 worker 啟動前檢查 migration（`DatabaseSchema`）：有未套用版本或模型與 snapshot 不一致就以退出碼 1 停止。`Persistence:ApplyMigrationsOnStartup` 預設 false，只做唯讀檢查。
- 測試用 SQLite 依目前模型建庫（`SqliteModel.cs` 處理向量與全文差異），不執行 SQL Server migration；真實 SQL Server 的測試方式見 [測試](../development/testing.md#真實-sql-server)。

## 初始化與權限

建庫、`migrations.sql`、SQL 帳號權限與 RCSI 切換見 [資料庫建置與權限](../operations/database-setup.md)。

## 手寫 SQL

EF Core 負責 mapping、migration 與業務寫入；EF 表達不了的 SQL 一律透過 `ISqlDatabase<T>`（`AiNexus.Platform/Data/Sql`，Dapper，只有非同步）。值用參數，物件名稱只取程式固定清單。

- `ISqlDatabase<NexusDbContext>`：主資料庫，共用 `NexusDbContext` 的連線與目前交易，與 EF 寫入一起提交或回復。
- `ISqlDatabase<NexusMasterDatabase>`：初始化建庫用，以 `Databases:Nexus` 連 `master`。
- `ISqlDatabase<GdwebDatabase>`／`ISqlDatabase<MeihoDatabase>`：外部來源，每次呼叫自開連線；設定須 `ReadOnly=true`，不取代 SQL 端 view-only 權限。

連線由 [`DatabaseOptions`](../../backend/src/AiNexus.Platform/Data/DatabaseOptions.cs) 組成。新增資料庫＝在擁有它的模組加一個 `ISqlDatabaseDefinition` 類別（`Name` 對應 `Databases.<Name>`）以 `AddSqlDatabase<T>()` 註冊，再於 `appsettings.json` 加設定；未宣告的項目會讓啟動失敗。schema 名稱要與上表一致。

## 保存與備份

- `RunEvents` 預設保留 24 小時供 SSE 重播，權威狀態在 `GenerationRuns`；未被引用的草稿附件依保留期清理；分享到期後清理快照。
- 軟刪除的對話、成果，以及稽核、評測資料的保存期由部署單位制定，目前沒有自動硬刪。
- 完整備份＝同一時點的 SQL＋站外附件目錄＋設定與 Data Protection key ring，步驟見 [BACKUP](../operations/backup.md)。
