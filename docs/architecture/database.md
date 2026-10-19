# 資料庫

- 業務資料在 SQL Server 2025 的 `AiNexus` 資料庫。**結構以開發庫為準**：DBA 在 SSMS 修改，匯出到 `database/App/` 建成 dacpac；EF Core 只做對應，沒有 migration（[ADR 0010](../decisions/0010-database-first-schema.md)～[0014](../decisions/0014-procedure-audit.md)）。
- 對應在 `backend/src/AiNexus.Data/App/`，由單一 `AppDbContext` 載入；跨模組外鍵與同一交易可直接使用。
- **一表一檔，資料夾＝schema**：`identity.Users` → `AiNexus.Data/App/Identity/User.cs`（實體＋`IEntityTypeConfiguration`）→ `database/App/identity/Tables/Users.sql`。實體名稱＝資料表單數名；唯一例外 `WebSearches` → `WebSearchRecord`，因為 `WebSearch` 與模組命名空間同名會無法引用。`DatabaseModelTests` 檢查資料夾與 schema 一致，且 Features 不得宣告組態。
- 欄位用到的列舉、狀態常數與值型別（`RunStates`、`UserPreferences`）跟著實體放在 `AiNexus.Data`；`AiNexus.Data` 只依賴 Platform，不能引用模組型別。

## 改結構

1. DBA 在開發庫修改（命名、外鍵、說明依 [ADR 0012](../decisions/0012-database-naming-and-foreign-keys.md)）。
2. `scripts/Export-Database.ps1` 匯出，只 stage 與本次有關的檔案；`dotnet build database/App` 要通過。
3. 同一個 PR 更新 `AiNexus.Data` 的實體與組態。

- **組態只寫對應需要的內容**：表、主鍵、長度、精度、型別、關聯與刪除行為、owned type、query filter、值產生器、並行控制欄位。索引、約束、預設值與說明只存在資料庫，不在 EF 重複宣告。
- 刪除行為要與資料庫外鍵一致：重要業務外鍵 `Restrict`，純附屬資料才 `Cascade`；EF 依它決定是否連帶刪除已追蹤的子列。
- 時間戳以 `CreationTime`（`AppDbContext.Clock`）產生，測試才能控制時間。
- 基礎資料（內建角色、群組、功能、預設選單）寫在 `database/App/Seed/`，不用 `HasData`；部署時在結構之後執行（[資料庫建置與權限](../operations/database-setup.md#部署)）。
- 網站啟動時 `DatabaseSchema` 以 `ModelSchema` 唯讀比對 EF 模型與 `sys.columns`：會讓程式失敗的差異（缺表或欄位、型別或 nullable 不同、多出沒有預設值的 NOT NULL 欄位）停止啟動並列出差異，其餘（資料庫先加的欄位、只經 SQL 存取的表）只記警告。完整結構的比對見 [結構驗證](../operations/database-verification.md)。

## Schema 與模組

一個業務模組一個 schema，名稱＝模組名小寫、不帶區（`accesscontrol`、`files`…）；共用的 `audit.AuditEvents` 由 `AuditEventInterceptor` 遮罩與補值，procedure 見[新增一支 procedure](#新增一支-procedure)。

## 欄位規則

- 能界定長度的字串一律 `HasMaxLength`，上限最好與 validator 共用常數（例如 `PromptTemplate.ContentMaxLength`）；超過 4000 字元的上限在 SQL Server 會是 `nvarchar(max)`，但上限仍由程式檢查。
- 時間用 `DateTimeOffset`（UTC）；金額 `decimal(20,8)`，未知費用保持 null。
- 原始附件不進資料庫，只保存站外儲存識別與 metadata（見 [附件保存](../features/attachment-storage.md)）。
- 只有 `Conversation`、`Resource` 套用具名 query filter `SoftDelete`；其他刪除是實體刪除。

## 授權關聯

使用者→角色→群組→功能見 [身分與授權](access-control.md)；資源（專案、知識庫、成果）共用 `collaboration` 的 `Resources`／`ResourceMembers`／`ResourceGroups` ACL。功能 grant 不等於資料 grant：knowledge 不授予全庫閱讀，專案不公開彼此的私人對話。

## 重要約束與索引（為什麼存在）

定義在 `database/App/`；程式依賴以下保證：

- `GenerationRuns`：`OwnerId`＋`IdempotencyKey` 唯一索引防重複送出；`ActiveOwnerId` 非空的 filtered unique index 限制每人一個進行中的生成；`ActiveOwnerId`＋`LeaseExpiresAt` 索引供租約復原。它與 `ModelInvocations` 的 `CreatedAt` 覆蓋索引供全平台用量報表依日期讀取。
- `RunEvents.CreatedAt` 供保留清理分批刪除（每批自動提交，避免鎖升級與長交易）；`Resources` 的 `Kind`＋`UpdatedAt`（INCLUDE `OwnerId`、`ParentId`、`IsDeleted`）供 `ResourceAccess` 依種類列出可讀資源。
- `Messages.ParentId` 形成訊息樹，`Conversations.ActiveLeafId` 決定目前分支；編輯與重新生成都新增訊息，不覆寫。
- `ArtifactRevisions` 以 `ArtifactId`＋`Version` 為主鍵；expected version 不符時不覆蓋他人的修改。
- `BackgroundJobs.ActiveKey` 唯一，避免同一業務重複排程；`LeaseToken` 做 fencing，過期 worker 不能提交。
- `EvaluationResults` 以 `RunId`＋`CaseIndex`＋`VariantIndex` 為主鍵，重試時跳過已完成的結果。
- `AuditEvents` 以 `At` 及 `Action`／`ResourceId`／`ActorId`＋`Id` 索引支援日期篩選與遞減游標；`DiagnosticEvents` 以 `At`＋`LogId` 為叢集索引，`LogId` 唯一，補送時去重。
- `knowledge.Chunks` 有 `KnowledgeSearch` 全文索引（繁中 1028 斷詞），腳本在 `database/App/FullText/`：`CREATE FULLTEXT` 不能在交易內執行，也不進 dacpac，部署時在結構更新後執行；缺全文元件或斷詞器時跳過，執行期改用 vector 模式。向量表與擴充方式見 [VECTOR_ARCHITECTURE](../research/vector-architecture.md)。

## 讀取隔離（RCSI）

資料庫開啟 `READ_COMMITTED_SNAPSHOT`：一般讀取取最後已提交版本，不等寫入。所以防併發只能靠會加鎖的做法：`Serializable`、`UPDLOCK`／`HOLDLOCK`、唯一索引、條件式 `ExecuteUpdate`，或先以無害 UPDATE 鎖擁有者列再查詢（查詢開始時才取快照）。啟動時只檢查並警告（`DatabaseSchema`）；既有庫的切換步驟與代價見 [資料庫建置與權限](../operations/database-setup.md)。

## 手寫 SQL

分工見 [ADR 0011](../decisions/0011-data-access-ef-dapper-procedures.md)；EF 表達不了的 SQL 一律透過 `ISqlDatabase<T>`（Dapper），值用參數。

- `ISqlDatabase<AppDbContext>`：主資料庫，共用 `AppDbContext` 的連線與目前交易。
- `ISqlDatabase<MasterDatabase>`：建庫用，以 `Databases:App` 連 `master`。
- `ISqlDatabase<GdwebDatabase>`／`ISqlDatabase<MeihoDatabase>`：外部來源，每次自開連線；設定須 `ReadOnly=true`，不取代 SQL 端唯讀權限。

連線由 [`DatabaseOptions`](../../backend/src/AiNexus.Platform/Data/DatabaseOptions.cs) 組成。新增唯讀來源＝在擁有它的模組加 `ISqlDatabaseDefinition` 類別（`Name` 對應 `Databases.<Name>`）以 `AddSqlDatabase<T>()` 註冊，再於 `appsettings.json` 加設定；未宣告的項目會讓啟動失敗。

### 新增一支 procedure

1. 依[改結構](#改結構)建立 `<schema>.usp_<UseCase>`；交易見 [ADR 0011](../decisions/0011-data-access-ef-dapper-procedures.md#交易)，稽核見 [ADR 0014](../decisions/0014-procedure-audit.md)。
2. 讀取用 `QueryProcedureAsync`，寫入用 `ExecuteProcedureAsync`（同批次帶稽核情境）；record 欄位名稱、順序、型別與 `SELECT` 一致。
3. 預期內的失敗：`THROW 50000, N'<error_code>', 1;`（`ProcedureError`）。
4. 測試用 `ProcedureAudits`。

範例：[`ListAdminUsers`](../../backend/src/AiNexus.Features/Admin/Operations/ListAdminUsers.cs)、[`CreateModelPrice`](../../backend/src/AiNexus.Features/Admin/Billing/Prices/CreateModelPrice.cs)。

## 測試

整合測試在 LocalDB 上以 dacpac 與 `Seed/` 建庫，EF 對應、資料表說明與唯一索引都對這個庫檢查；見 [測試資料庫](../development/test-database.md)。

## 保存與備份

- `RunEvents` 預設保留 24 小時供 SSE 重播，權威狀態在 `GenerationRuns`；未被引用的草稿附件依保留期清理；分享到期後清理快照。
- 軟刪除的對話、成果，以及稽核、評測資料的保存期由部署單位制定，目前沒有自動硬刪。
- 完整備份＝同一時點的 SQL＋站外附件目錄＋設定與 Data Protection key ring，步驟見 [BACKUP](../operations/backup.md)。
