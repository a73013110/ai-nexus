# 資料庫與 schema

`20261007040053_SystemDiagnostics` 為增量升級，新增結構化診斷表、Job／Run 的持久關聯、訊息／通知／SSE 查證代碼與稽核關聯，承接 WorkspaceExperience 基線。已存在的資料庫套用新的 `db/migrations.sql` 或由 `Initialize-Database.ps1` 執行待處理 migration，不重新建立基線。EF model、物件描述、OpenAPI／前端 schema 同步更新。

DiagnosticEvents 的 LogId 是非叢集唯一主鍵，`At DESC, LogId DESC` 是叢集索引；等級／Category／EventId／Name 及查證／流程識別有複合索引。查證不依賴中文全文元件。SQL import 使用獨立連線與 transaction；30天診斷與365天稽核各自分批清理，均可配置。應用需要 DiagnosticEvents 的 SELECT／INSERT／受控 retention DELETE，以及既有 AuditEvents 寫入與清理權限；DDL 仍由部署帳號執行。完整政策與容量限制見 [DIAGNOSTICS](DIAGNOSTICS.md)。

業務資料集中在 **AiNexus** SQL Server database。schema 是資料庫內的命名空間，例如 `[access].[Roles]`，不是另一個 database 或另一條連線。按模組分 schema，讓責任、migration 與 SQL 授權容易辨識；跨 schema 外鍵及同一個 EF transaction 仍可使用。

已有 SQL 登入可直接使用，不必另建同名帳號。一般參數在 .local/config/appsettings.Local.json，密碼在 .local/secrets/appsettings.Secrets.json。Initialize-Database.ps1 用既有登入建立不存在的 AiNexus，再套用未完成版本；無建庫／DDL 權限時由 DBA 先建庫及套用 SQL。見 [參數](CONFIGURATION.md)。

## Schema 與主要物件

| Schema        | 主要物件                                                                  | 責任                                                        |
| ------------- | ------------------------------------------------------------------------- | ----------------------------------------------------------- |
| identity      | Users、UserPreferences                                                    | AD SID、帳號與個人偏好，不保存個人 AD 密碼                  |
| access        | Roles、RoleGroups、Features、UserRoles、RoleGroupRoles、RoleGroupFeatures | 使用者→角色→群組→功能、Enabled／有效授權                    |
| access        | AdministratorBootstraps、GroupModelPolicies、UserModelPolicies            | 一次性 bootstrap、個人與群組模型／每日 token／儲存限制      |
| conversations | Conversations、Messages、ConversationLabels                               | 私人訊息樹、目前分支、指令、收藏／封存／標籤                |
| inference     | GenerationRuns、RunEvents、ModelProfiles、ModelInvocations                | 執行參數／冪等、SSE replay、能力、OCR／文字／embedding 用量 |
| inference     | ModelPrices、ModelCharges、WebSearches                                    | 不可變價格版本、呼叫價格／用量快照、搜尋來源與冪等          |
| workspace     | RepositoryConnections                                                     | 每個人自己的 Gitea 帳號與 Data Protection 加密 token        |
| operations    | AuditEvents、BackgroundJobs、DiagnosticEvents                             | 獨立稽核政策、durable 工作、非業務交易批次診斷保存          |
| attachments   | Attachments、MessageAttachments、ResourceAttachments                      | 站外原檔 metadata、儲存識別、文字、引用及配額               |
| library       | PromptTemplates                                                           | 個人提示詞，最多 100 個                                     |
| collaboration | Resources、ResourceMembers、ResourceGroups                                | 擁有者、具名 viewer／editor、群組唯讀、ParentId 繼承        |
| collaboration | ShareLinks、ShareRecipients                                               | 到期／撤銷、具名收件人、版本快照及明確附件授權              |
| knowledge     | Collections、Documents、DocumentPages、Chunks                             | 頁面、OCR 狀態、索引 profile、片段／向量                    |
| knowledge     | EmbeddingProfiles、ChunkEmbeddings768、ChunkEmbeddings1024                | profile 生命週期、真正 VECTOR(n)、內容 hash 快取與覆蓋率 |
| knowledge     | ConversationCollections、MessageCitations                                 | 對話選定來源及回答當時的文件／頁碼／摘要                    |
| knowledge     | RepositoryImports                                                         | 固定 Gitea commit／path／host 與知識庫文件來源識別          |
| content       | Artifacts、ArtifactRevisions、SourceReferences                            | 成果不可變版本、目前版本、來源識別／版本／時間              |
| projects      | Projects、ProjectTemplates                                                | 共用指示、專案版本及範本，文件／成果透過 Resources 關聯     |
| quality       | MessageFeedback、EvaluationSets、EvaluationRuns、EvaluationResults        | 私人回饋、固定題庫、執行設定及逐題結果／人工評分            |
| quality       | RetrievalEvaluations、RetrievalEvaluationResults                          | 固定檢索驗收集及四模式相關性、拒答、延遲指標 |
| dbo           | \_\_EFMigrationsHistory                                                   | 已套用的 EF 版本，不可手改或刪除以重跑 migration            |

共有 13 個業務 schema，以 InitialCreate 基線與增量 migrations 管理。原始附件存於站外 Attachments.StoragePath，SQL 不保存原始 bytes。StorageKey 唯一索引與狀態／時間索引支持存取及回收；Users.AttachmentLimitBytes 為個人容量 override，null 繼承群組／預設 5 GB，DB 檢核非負及安全上限。個人偏好為 UserId 的 1:1 關聯；API key／SQL／AD 服務密碼不在偏好表，Gitea token 獨立加密保存。

## 物件描述與版本維護

`AiNexus.Features/Persistence/DatabaseDescriptions.cs` 是資料表及欄位描述的唯一模型來源；新增映射物件漏寫說明會在建立模型時被攔下。EF migration 將其寫入 SQL Server `MS_Description`。`db/object-descriptions.sql` 可重跑，補上 schema、實體索引、主鍵／外鍵／唯一／預設／檢核約束、原生向量欄位及 EF 版本表說明；不變更業務資料。明確執行初始化時會重套用此內嵌 SQL，涵蓋後續新增的索引與約束，一般啟動不執行 DDL。新增物件的 migration 應同步更新其描述。執行 `scripts/Test-DatabaseDescriptions.ps1` 可唯讀檢查部署後是否有遺漏；初始化也會自動檢查。

InitialCreate 包含目前登入政策、Argon2id 雜湊、登入撤銷版本、測試身分稽核、物件描述及全部功能種子。基本群組名稱為「基本工作區」，本地登入須由管理者明確設定。

本版將累積 migrations 合併成 `20261005171400_InitialCreate`，僅適用全新空資料庫，沒有舊 binary 遷移或舊資料庫相容。初始化不會 DROP 或清空現有 DB；遇到其他基線的 history 會停止，不可刪 history 硬套新基線。日後模型變更追加具名 migration，不再任意重置正式基線。

## 授權關聯

```mermaid
erDiagram
    Users ||--o{ UserRoles : assigned
    Roles ||--o{ UserRoles : belongs
    Roles ||--o{ RoleGroupRoles : joins
    RoleGroups ||--o{ RoleGroupRoles : contains
    RoleGroups ||--o{ RoleGroupFeatures : grants
    Features ||--o{ RoleGroupFeatures : available
    Users ||--o{ Conversations : owns
    Conversations ||--o{ Messages : contains
    Conversations ||--o{ GenerationRuns : generates
    GenerationRuns ||--o{ RunEvents : replays
    Resources ||--o{ ResourceMembers : names
    Resources ||--o{ ResourceGroups : permits
    Resources ||--o| Projects : specializes
    Resources ||--o| Collections : specializes
    Resources ||--o| Artifacts : specializes
    Artifacts ||--o{ ArtifactRevisions : versions
    EvaluationSets ||--o{ EvaluationRuns : compares
    EvaluationRuns ||--o{ EvaluationResults : checkpoints
```

首次登入在同一 transaction 建立 Users 與 member。本版完整 migrations 後，member→workspace 提供 dashboard、chat、projects、knowledge、artifacts、shared、quality、tasks、repositories；admin／integrations 預設只授予 administrators。既有帳號登入不重新授予被撤銷角色。個人設定依登入帳號可用，不另建秘密表。bootstrap 完成一次即留下標記，防止撤銷後又因登入取得管理權。

**功能 grant 不等於資料 grant**：knowledge 不授予全庫閱讀，專案不公開彼此私人聊天。Resources.ParentId 提供一層專案繼承，具名 editor 可寫、群組唯讀。ShareRecipients 與原資源 ACL 分開。外部來源還需完整 SID／account 授權。

## 歷史與重要索引

- Messages.ParentId 形成訊息樹，Conversations.ActiveLeafId 決定目前路徑。編輯新增 user，重新生成新增 assistant sibling，停止／失敗保存部分回答。
- GenerationRuns 的 OwnerId＋IdempotencyKey 唯一索引防重複送出；ActiveOwnerId 非空的 filtered unique index 限制每人一個 active run。
- GenerationRuns 保存 ExecutorId、LeaseExpiresAt 與 ActiveOwnerId＋LeaseExpiresAt 索引。租約每 15 秒續約，兩分鐘未續約才判定 executor 中斷，保留部分回答與歷史事件。
- GenerationRuns 凍結 Provider、ProviderModelId 與核准路由 ModelId；完成／取消／失敗保存 DurationMilliseconds、GenerationMilliseconds 與 InputTokens／OutputTokens。ModelInvocations 也保存 provider 與耗時，管理分析不用解析 audit JSON。執行耗時包含 Context 準備及等待 provider 容量，不能直接視為模型純輸出速度。
- ArtifactRevisions 的 ArtifactId＋Version 複合主鍵保存版本；expected version 衝突不覆蓋他人的異動。
- BackgroundJobs 保存 ActiveKey、LeaseToken／期限、階段／完成量及 attempt；checkpoint 經 fencing，過期 worker 不能提交。
- EvaluationResults 的 RunId＋CaseIndex＋VariantIndex 複合主鍵支援重試跳過已完成結果。VariantsJson 保存模型設定及指紋，不含 key／密碼。
- ShareLinks 按擁有者／期限索引；撤銷、到期及原始刪除停止閱讀，清理快照與附件引用。
- AuditEvents 以 At、Action＋Id、ResourceId＋Id 索引支援日期篩選與遞減游標。DetailsJson 保存管理異動前後狀態或唯讀檢視範圍，不保存秘密、搜尋文字或對話內容。
- 重要業務外鍵採 Restrict，避免刪使用者／專案造成歷史連鎖刪除；純附屬資料依明確策略處理。

搜尋目前以 owner 限制下的 SQL substring 查詢，未建立 Full-Text Catalog；資料量增大可保留 API 再加全文搜尋。Context 只裁切此次提供模型的上文，不刪歷史，預估與實測 tokens 分開保存。

UsageReports 共用 GenerationRuns／ModelInvocations 的 SQL 聚合查詢，提供平台、各使用者與個人設定的用量；使用者清單批次取得用量，避免逐人查詢。管理員可透過獨立且受稽核的唯讀 endpoint 檢視使用者的所有對話版本（包含封存及選擇顯示的已刪除對話），一般對話 API 仍維持擁有者隔離。

## 向量與外部來源

本版僅支援 SQL Server 2025（17.x），EF Core 10 原生映射 SqlVector<float>。knowledge.EmbeddingProfiles 保存不可變空間／切段快照；Chunks 保存單一切段布局及唯一 SearchId，ChunkEmbeddings768／1024 分別使用 VECTOR(n)、int identity clustered PK、profile/hash 索引及 profile/chunk 唯一鍵。舊 EmbeddingJson／EmbeddingVector 欄位及 portable cosine 查詢已移除。LANGUAGE 1028 全文索引在交易外建立，缺少元件時跳過並警示；精確向量依授權候選先過濾，不開 ANN preview。見 [架構及擴充](VECTOR_ARCHITECTURE.md)。

content.SourceReferences 保存明確匯入的個人成果之 SourceId／ExternalId／Revision／ImportedAt。公文／校務仍為獨立來源庫，透過 nexus.AuthorizedRecords／AuthorizedRecordHistory view 與來源專用唯讀登入，以完整 SID／account 取得資料。來源版本變更要求重新讀取；已明確保存的個人副本不因來源撤權自動遠端抹除。AiNexus migration 不在外部庫建物件，DBA 範本在 db/integrations。見 [來源契約](INTEGRATIONS.md)。

## EDoc、初始化及 SQL 權限

保留 [EDoc 原始 helper](../backend/src/AiNexus.Platform/Data/EDoc/README.md)。EF Core／IEfHelper 管 mapping、migration、業務寫入及共用 scoped context。Dapper IDbHelper 用於固定參數化 SELECT、狀態及建庫；自有連線不自動參與 EF transaction。值用 parameters，物件名稱只取程式固定清單。

NexusConnectionFactory 以 marker 對應 AiNexus、CLI 專用 master，以及 LegacyGdweb／LegacyMeiho。來源連線加密及唯讀意圖不取代 SQL 的 view-only 權限。

```powershell
./scripts/Initialize-Database.ps1
```

工具只在 AiNexus 不存在時建庫，適用空資料庫或相同基線的未完成版本。DBA 可先建空 AiNexus，再執行 [idempotent SQL](../db/migrations.sql)，其中沒有 CREATE LOGIN／DATABASE 或秘密。source、designer、snapshot 位於 backend/src/AiNexus.Features/Persistence/Migrations，包含 InitialCreate 與後續增量版本。正式 DDL 使用獨立部署帳號。

```powershell
dotnet ef migrations list --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Api
dotnet ef migrations has-pending-model-changes --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Api
```

初始 migration 保持原樣，後續升級追加四個版本：20261006144804_VectorRetrievalProfiles、20261006145833_SingleChunkLayout、20261006150830_CitationPageRanges、20261006155137_RetrievalEvaluationReports。第一個清空舊片段、標記知識文件 reindex，原始附件與 DocumentPages 保留；後續建立單一布局、引用頁碼範圍及評測表。沒有舊索引兼容查詢；本次更新尚須在部署環境套用 migration 並重新上傳或重建。初始化及正常啟動檢查模型與 snapshot 一致。

已設定 SQL 的 host 會在 HTTP 與背景 worker 啟動前檢查所有 migration；缺少任何版本會以退出碼 1 停止，列出待套用的版本及初始化方式。`Storage.ApplyMigrationsOnStartup=false` 仍會執行唯讀版本檢查，不會修改 schema。啟動、初始化與連線／部署驗證共用 `DatabaseSchema`，新增 migration 不必另加欄位特例。測試的 SQLite 使用當前模型建庫，不執行 SQL Server migrations。

本版管理與一般 endpoint 共用 Nexus 連線，runtime 登入需要上述 13 個業務 schema 的 SELECT／INSERT／UPDATE／DELETE，也包括 bootstrap／管理異動的 access 物件；版本檢查另需 `dbo.__EFMigrationsHistory` 的 SELECT，不需修改 history 的權限。實際操作由後端政策控制。**目前沒有管理專用寫入連線**，不能只給 access SELECT／首次登入 INSERT 就預期後台可運作。runtime 不給 master 建庫、ALTER schema 或 db_owner；進一步分離管理 SQL 權限需要實作獨立連線及交易邊界。外部來源登入則只授兩個固定授權 view 的 SELECT。

## 保存與備份

`quality.RetrievalEvaluations` 保存私人驗收集、collection IDs、配置／文件版本指紋及 job；`RetrievalEvaluationResults` 以 run/case/mode 為鍵，只保存指標、實際模式、錯誤代碼及延遲。下載報告不含問題、標註文件 ID、向量或來源文字，仍須重新通過 owner、quality 與知識庫 ACL。所有新表／欄位都有繁中 MS_Description，DBA 套用 migrations 後執行 Test-DatabaseDescriptions 檢查。

真實 SQL 整合測試需明確指定測試 instance（登入須能建立／刪除測試資料庫及全文 catalog），每項建立隨機 AINexus_Retrieval_Test_ 資料庫，只清理該項自行建立的資料庫，不使用連線字串指定的現有資料庫。

```powershell
$env:AINEXUS_SQLSERVER_TEST = 'Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
dotnet test backend/tests/AiNexus.Tests --filter FullyQualifiedName~SqlServerRetrievalTests
```

測試驗證原生 768／1024 向量、Dapper 批次與交易回滾、profile 隔離、1028 中文 FREETEXTTABLE、授權範圍先於 TOP，以及全文執行錯誤時 hybrid 降為 vector、keyword 回報明確錯誤；全文非同步填入輪詢上限 90 秒。未提供環境變數時明確 skip，不以 SQLite 代替真實 SQL 驗證。

RunEvents 預設保留 24 小時 replay，權威 run 快照仍可恢復；未保存到檔案庫且未被訊息／資源／分享引用的草稿附件依保留期清理。檔案庫原檔需沒有引用後由擁有者明確刪除。分享到期可清理快照，soft-delete 對話、成果、audit 與評測等保存期由部署單位制定，再加入明確 retention。

完整備份包含全部 schema 與同一時點的站外附件目錄，只備份 SQL 無法還原原檔。config 及 Data Protection key ring 另受 ACL 保護。停機一致性備份與還原校驗見 [BACKUP](BACKUP.md)，須在隔離環境核對 SID、角色、ACL、訊息樹、版本、索引 profile、原檔及跨帳號隔離。

## 費用、搜尋與 1024 維向量

InitialCreate 包含 ModelPrices／ModelCharges／WebSearches、RepositoryConnections／RepositoryImports、dashboard／repositories 功能種子及 SQL Server 2025 的 1024 維欄位。價格快照為 decimal(20,8)，未知費用保持 null；呼叫 ID 是主鍵，避免重試重複計費。價格 ModelId 為供應商原生 ID，核准路由另含 provider。

報表在 SQL 彙總 owner／日期／模型／幣別／類型，不讀取所有訊息內容；查詢上限 366 天，使用開始含／結束不含。費用與搜尋表有 owner／created time 索引，價格有 provider／model／effective time 唯一索引，搜尋有 owner／idempotency key 唯一索引。Gitea token 用 Data Protection 加密，備份 SQL 時需同時保存 key ring 與保護身分；否則 token 需重新連線。

詳見 [費用](BILLING.md)、[搜尋](WEB_SEARCH.md)、[Gitea](GITEA.md)、[模型比較](EMBEDDING_MODELS.md)。

## 檔案庫與容量

Attachments.InLibrary 管理原檔保存；同一原檔多個引用只計一次。容量以 Size 加總，包含草稿、預約及刪檔重試；上傳／引用異動使用使用者 SQL row lock 協調程序。先 commit metadata 狀態再做 IO，實體刪檔成功才刪 metadata 及釋放配額。見 [附件](ATTACHMENTS.md)、[檔案庫](FILES.md)。

`access.UserModelPolicies` 保存個人模型白名單與逐模型每日 token 覆寫；`GroupModelPolicies.DailyTokenLimitsJson` 保存群組預算。`GenerationRuns` 與 `ModelInvocations.ReservedTokens` 用於原子預約與缺失用量保護，不是實際 token 計量。舊每日請求次數由 PerModelTokenBudgets migration 移除。
