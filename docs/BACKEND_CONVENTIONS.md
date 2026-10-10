# 後端撰寫慣例

本文件說明 `AiNexus.Features` 內一個 use case（slice）的寫法。範本：`Library`（最小）、`Notifications`、`Integrations`（含授權閘道）、`Billing`（validator 注入服務）。

## 檔案與命名

- 一個 use case 一個檔案，檔名是動詞片語：`SavePromptTemplate.cs`、`ListNotifications.cs`。同一資源、共用大量邏輯的唯讀查詢可以放在同一檔（如 `BrowseSources.cs`）。
- 模組根目錄的 `<Module>Module.cs` 建立路由群組（授權、tags、body 上限），再依序呼叫各 slice 的 `Map`。端點順序就是 OpenAPI 順序，不要任意調整。模組的功能入口也在這個檔案宣告成 `FeatureSeed` 子類別。
- 檔名＝檔內主要型別名，namespace＝資料夾（`SourceLayoutTests` 檢查）。entity 與它的 `IEntityTypeConfiguration<T>`、查詢擴充方法同檔（如 `PromptTemplate.cs`）。
- 組態類別要有無參數建構子，`NexusDbContext` 以 `ApplyConfigurationsFromAssembly` 自動套用，新增實體只要加這一個檔案。種子資料用該模組組態的 `HasData`；另一端在別的模組的外鍵寫在 `Persistence/CrossModuleRelationships.cs`，只有 SQLite 才需要的差異寫在 `Persistence/SqliteModel.cs`。
- DTO 只有一個 use case 用就寫在該 use case 檔的開頭；多個檔案共用才獨立成 `<Dto>.cs`。
- 模組的預期失敗集中在根目錄的 `<Module>Errors.cs`（`ModuleBoundaryTests` 檢查）。
- 超過約 20 個檔案的模組依能力分子資料夾（如 `Knowledge/Documents`、`Identity/Sessions`）；`<Module>Module.cs`、`<Module>Errors.cs`、options 與跨子資料夾共用的型別留在模組根目錄。
- slice 類別預設 `internal`；只有其他模組要用的服務（如 `NotificationService.PublishAsync`）才是 `public`，這就是模組的對外合約。

## 建置規則

- `AnalysisLevel=latest-recommended`＋`EnforceCodeStyleInBuild`，warning 即錯誤。整體關掉的規則與原因集中在根目錄 `.editorconfig`；單一型別的例外用寫明 `Justification` 的 `[SuppressMessage]`，不用 `#pragma`。
- NuGet 版本只寫在 `backend/Directory.Packages.props`，`PackageReference` 不帶版本；改版本後 restore 更新各專案的 `packages.lock.json`。

## 端點與 handler

```csharp
internal sealed class SavePromptTemplate(NexusDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, null, body, ct).ToHttpResultAsync())
        .WithName("CreatePromptTemplate");

    public async Task<Result<PromptTemplateDto>> HandleAsync(...) { ... }
}
```

- 每個 use case 都是 `internal sealed class`：靜態 `Map*(RouteGroupBuilder)` 宣告端點，實例 `HandleAsync`（同步時 `Handle`）做事，相依由建構子注入。`EndpointHandlers` 依這個形狀自動註冊為 scoped，模組的 `AddServices` 只註冊共用服務（`SliceTests` 檢查形狀）。同一資源有多個端點時用多個 `Map*` 與具名方法（如 `BrowseRepositories`）。
- lambda 回傳 `TypedResults`：`Result` 用 `ToHttpResultAsync()`（成功 200／204，失敗 problem）；成功不是 200 時傳 mapper，如 `ToHttpResultAsync(x => TypedResults.Accepted(...))`。回應型別由回傳型別進入 OpenAPI，不寫 `.Produces<T>()`；只有 SSE（`text/event-stream`）與自行寫入回應的下載例外。
- lambda 直接回傳 `ToHttpResultAsync()` 時不要加 `async`，否則回傳型別變成 `Task<Task<…>>`，OpenAPI 產生錯誤的 schema。
- SSE 端點用 `ToStreamResultAsync(http)`：串流開始前的失敗回 problem，開始後改寫一個 `error` 事件。
- 使用者一律用 `ICurrentUser`（同步 `Id`），不要在新程式呼叫 `CurrentUser.GetAsync`。
- 時間一律用注入的 `TimeProvider`；`DateTimeOffset.UtcNow`、`DateTime.Now` 等由 BannedApiAnalyzers 擋下（清單在 `backend/src/BannedSymbols.txt`）。實體的建立時間在 `Add` 時由 `CreationTime` 從 `NexusDbContext.Clock` 補上；`Add` 之前就要讀取時間的程式自行設定。
- 直接使用 `NexusDbContext`；不要新增 repository 或只轉送呼叫的 service。
- 一個使用者能放大成本的端點（送出、上傳、外部呼叫、匯出）在模組 `AddServices` 用 `options.AddPerUserLimit(名稱, 每分鐘次數)` 註冊，端點加 `.RequireRateLimiting(名稱)`。
- 日誌寫成 `[LoggerMessage]` 方法，EventId 固定且唯一，見 [LOG_EVENTS](LOG_EVENTS.md)；直接呼叫 `LogWarning` 等會編譯失敗。

## 錯誤

- 預期內的失敗（找不到、衝突、無權限、配額）回傳 `Result<T>`／`Result`，錯誤定義在 `<Module>Errors`：`Error.NotFound("template_not_found")`。代碼必須存在於 `PublicErrorCatalog`，前後端提示由 `DiagnosticTests` 檢查一致。
- HTTP 狀態碼只由 `Problems.Status(ErrorKind)` 決定。
- 不要為預期內的失敗丟例外。呼叫端傳遞失敗：`if (await X(...) is { IsSuccess: false } failed) return failed.Error;`；背景工作的 `ExecuteAsync` 回傳的錯誤會記錄成任務的錯誤代碼。
- 外部系統故障或拒絕（模型供應商、Gitea、外部資料庫）發生在無法回傳 `Result` 的位置（HTTP client、串流中途、探測）時，丟 `ExternalServiceException(Error, detail)`：狀態碼依 `Error.Kind`，`detail` 只進日誌。
- 其他例外（資料庫連線、程式錯誤）由診斷 middleware 記錄並回 503 與 issue code。
- `T` 是介面時用 `Result<T>.Ok(value)`（C# 不允許介面的隱含轉換）。

## 請求驗證

- request body 的格式規則寫成 `RequestValidator<T>`，與 request record 放在同一個 slice 檔。驗證 filter 已掛在 `/api/v1` 群組，handler 不必再檢查。
- 要保留模組既有的錯誤代碼時覆寫 `ProblemCode`；欄位規則代碼用 `WithErrorCode("snake_case")`。回應的 `errors` 只列欄位與規則代碼，不含輸入內容。
- 需要資料庫的規則（重複、配額、擁有權）屬於 handler，不放 validator。
- `EndpointConventionTests` 以 `request-validators.baseline.txt` 管控尚未有 validator 的 request，新增 request 必須有 validator；補上後刪掉該行。

## 軟刪除

- `Conversation`、`WorkspaceResource` 在各自的 `IEntityTypeConfiguration` 宣告具名 query filter：`HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted)`（`AiNexus.Platform.Data.SoftDelete`）。一般查詢、join 與子查詢預設不含已刪除列，不必再寫 `!x.IsDeleted`。join 到這兩個 entity 的查詢也會一併濾掉，改寫 join 時要確認語意。
- 需要看到已刪除列的查詢以名稱退出：`IgnoreQueryFilters([SoftDelete.Filter])`，不要用無參數版本（會連其他 filter 一起關掉）。退出作用於**整個查詢**，同一查詢裡其他有 filter 的 entity 也會看到已刪除列，必要時自行補條件。適用：管理端稽核讀取、容器刪除時清除已刪除子項的連結、背景任務收尾（對話或文件在處理中被刪除）、清理／保留工作。
- 運算式樹內（`Where(x => ...)` 的子查詢）不能寫集合運算式，先在外面宣告 `var rows = db.Set<T>().IgnoreQueryFilters([SoftDelete.Filter]);` 再引用。
- `KnowledgeDocument`、`NexusUser.DeletedAt` 沒有 filter，仍明確寫條件：文件與其 `WorkspaceResource` 同步刪除，文件本身的 `IsDeleted` 是判斷依據；刪除的使用者保留供稽核與歷史顯示。Dapper／原生 SQL 不受 filter 影響，必須自行加條件。

## 授權

- policy 名稱用 `Policies.*` 或模組常數，不要寫字串。`EndpointConventionTests` 檢查每個端點都宣告授權，且每個 policy 都已註冊。

## 跨模組

- 只透過對方模組的 `public` 服務或 DTO。模組之間不可有循環依賴；造成循環時的處理順序，以及何時直接呼叫、何時用 domain event，見 [模組邊界](MODULE_BOUNDARIES.md)。
