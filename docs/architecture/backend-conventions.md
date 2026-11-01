# 後端撰寫慣例

`AiNexus.Features/<Area>/<Module>/` 內一個 use case（slice）的寫法。範本：`Prompts`、`Notifications`、`Sharing`（含授權閘道 `ShareAccess`、能力子資料夾）、`Billing`。

## 檔案與命名

- 一個 use case 一個檔案，檔名是動詞片語：`SavePromptTemplate.cs`、`ListNotifications.cs`。同一資源、共用邏輯的唯讀查詢可放同一檔（`BrowseProjects.cs`）。
- 模組根目錄的 `<Module>Module.cs` 建立路由群組（授權、tags、body 上限），再依序呼叫各 slice 的 `Map`。端點順序＝OpenAPI 順序。
- 檔名＝檔內主要型別名，namespace＝資料夾（`SourceLayoutTests` 檢查）。
- 實體與組態放 `AiNexus.Data/App/<Module>/`（不分區），見 [資料庫](database.md)。模組的查詢擴充（`<Entity>Queries`）與 DTO 對應（`ToDto`）放在模組內。
- DTO 只有一個 use case 用就寫在該檔開頭，多檔共用才獨立成檔。
- 模組超過 15 個檔案就依能力分一層子資料夾：
  - 根目錄只留橫跨模組的：`<Module>Module`、`<Module>Errors`、options、對外合約服務、總覽來源、write lock、多個能力共用的 entity。
  - 其餘放進擁有它的能力，如 `Inference/Runs`、`Files/Storage`；模組本身資源的 CRUD 放 `Management/`。
  - 名稱用 PascalCase 名詞，不取其他模組的名字（如 `Chat/Transcripts`）。測試只依模組分資料夾。
- slice 類別預設 `internal`；只有其他模組要用的服務（如 `NotificationService`）才 `public`，跨模組只透過它、不可循環，見 [模組邊界](module-boundaries.md)。

## 建置規則

- `AnalysisLevel=latest-recommended`＋`EnforceCodeStyleInBuild`，warning 即錯誤。整體關掉的規則與原因集中在根目錄 `.editorconfig`；單一型別的例外用寫明 `Justification` 的 `[SuppressMessage]`，不用 `#pragma`。
- NuGet 版本只寫在 `backend/Directory.Packages.props`；改版本後 restore 更新 `packages.lock.json`。
- BannedApiAnalyzers 擋下系統時鐘與執行期反射，清單與替代做法在 `backend/src/BannedSymbols.txt`（[ADR 0030](../decisions/0030-no-runtime-reflection.md)）。

## 端點與 handler

```csharp
internal sealed class SavePromptTemplate(AppDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, null, body, ct))
        .WithName("CreatePromptTemplate");

    public async Task<Result<PromptTemplateDto>> HandleAsync(...) { ... }
}
```

- use case 是 `internal sealed class`：靜態 `Map*(RouteGroupBuilder)` 宣告端點，`HandleAsync`（同步時 `Handle`）做事，相依由建構子注入。註冊（scoped）與掛載由[產生器](source-generators.md)處理，漏掛建置失敗；`AddServices` 只註冊共用服務。同一資源有多個端點時用多個 `Map*` 與具名方法。
- lambda 直接回傳 handler 的 `Result`／`Result<T>`：它們本身是 `IResult`（成功 200／204，失敗 problem），回應型別也由它們進入 OpenAPI，不寫 `.Produces<T>()`；只有 SSE 與自行寫入回應的下載例外。成功不是 200 或要回檔案時傳 mapper，如 `ToHttpResultAsync(x => TypedResults.Accepted(...))`。
- lambda 直接回傳 `Task` 時不加 `async`／`await`：只加 `async` 型別會變成 `Task<Task<…>>`，OpenAPI 錯誤。
- SSE 端點用 `ToStreamResultAsync(http)`：串流開始前的失敗回 problem，開始後改寫一個 `error` 事件。
- 使用者一律用 `ICurrentUser`（同步 `Id`），不呼叫 `CurrentUser.GetAsync`。
- 建立時間在 `Add` 時由 `CreationTime` 從 `AppDbContext.Clock` 補上；`Add` 前就要讀取的自行設定。
- 依字串鍵挑實作（工作種類、供應商）時以 `AddKeyedService`（或 `AddBackgroundJobHandler`、`AddRemoteFileSource`）註冊、注入 `KeyedServices<T>`：只建立查到的實作，鍵重複時啟動失敗；不注入 `IEnumerable<T>` 再比對。
- 直接用 `AppDbContext` 的 `db.<表名>`，不加 repository 或只轉送呼叫的 service；屬性名＝表名（view 去掉 `vw_`），新表在 `AppDbContext` 加一行（`DatabaseModelTests` 檢查）。單表用 EF，多表查詢、報表與 procedure 見 [ADR 0011](../decisions/0011-data-access-ef-dapper-procedures.md)；列表排序、篩選與分頁用 `TableRequest`＋`ToTablePageAsync`（[ADR 0026](../decisions/0026-shared-table-queries.md)）。
- 使用者能放大成本的端點（送出、上傳、外部呼叫、匯出）在 `AddServices` 以 `options.AddPerUserLimit(名稱, 每分鐘次數)` 註冊，端點加 `.RequireRateLimiting(名稱)`。匯出檔見 [匯出檔案](exports.md)。
- 日誌寫成 `[LoggerMessage]` 方法，EventId 固定且唯一，見 [LOG_EVENTS](log-events.md)。
- 授權用 `.RequireFeature(FeatureCatalog.X)`，每個功能的 policy 由 `FeatureCatalog.Grants` 註冊；多個頁面共用的 any-of policy 用 `Policies.*` 或模組常數，不寫字串。`EndpointConventionTests` 檢查 policy 已註冊。

## 錯誤

- 預期內的失敗（找不到、衝突、無權限、配額）回傳 `Result<T>`／`Result`，錯誤定義在 `<Module>Errors`：`Error.NotFound("template_not_found")`。
- 錯誤只宣告成靜態欄位：Features 放 `<Module>Errors`，Platform／Data／Host 放擁有它的型別（如 `TableQuery.Invalid`）；不寫行內 `Error.X("…")`、不以字串參數組錯誤。4xx 代碼必須有 `PublicErrorCatalog` 提示（皆由 `ErrorDefinitionTests` 檢查），前後端提示一致（`PublicErrorCatalogTests`）。
- 通用代碼（`invalid_request`、`not_found`、`rate_limited` 等）用 `HttpErrors`，模組不再宣告同代碼的錯誤。`Issues.Report(例外, Error)` 只寫日誌的代碼也是靜態欄位，類別用 `Unavailable`（不需提示）。
- HTTP 狀態碼只由 `Problems.Status(ErrorKind)` 決定。
- 呼叫端傳遞失敗：`if (await X(...) is { IsSuccess: false } failed) return failed.Error;`；背景工作 `ExecuteAsync` 回傳的錯誤記為任務的錯誤代碼。
- 外部系統故障或拒絕、又無法回傳 `Result`（HTTP client、串流中途、探測）時，丟 `ExternalServiceException(Error, detail)`：狀態碼依 `Error.Kind`，`detail` 只進日誌。
- 其他例外（資料庫連線、程式錯誤）由診斷 middleware 記錄並回 503 與 issue code。
- 清單回傳 `Result<T[]>`：`Result<T>` 的隱含轉換不接受介面型別，用 `IReadOnlyList<T>` 就得寫 `.Ok(...)`。

## 請求驗證

見 [請求驗證](request-validation.md)：驗證器、欄位規則代碼、單行與可換行文字的共用規則。

## 軟刪除

- `Conversation`、`Resource` 在各自的 `IEntityTypeConfiguration` 宣告具名 query filter：`HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted)`。查詢、join 與子查詢預設排除已刪除列；改寫 join 時確認語意。
- 需要看到已刪除列的查詢以名稱退出：`IgnoreQueryFilters([SoftDelete.Filter])`，不用無參數版本（會關掉其他 filter）。退出作用於**整個查詢**（其他有 filter 的 entity 也會看到已刪除列），必要時自行補條件。適用：稽核讀取、容器刪除時清除子項連結、背景任務收尾、清理／保留工作。
- 運算式樹內（`Where(x => ...)` 的子查詢）不能寫集合運算式，先在外面宣告 `var rows = db.<表名>.IgnoreQueryFilters([SoftDelete.Filter]);` 再引用。
- `Document`、`User.DeletedAt` 沒有 filter，要明確寫條件（刪除的使用者保留供稽核）。Dapper／原生 SQL 不受 filter 影響，必須自行加條件。
