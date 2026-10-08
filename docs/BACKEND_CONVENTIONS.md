# 後端撰寫慣例

本文件說明 `AiNexus.Features` 內一個 use case（slice）的寫法。範本：`Library`（最小）、`Notifications`、`Integrations`（含授權閘道）、`Billing`（validator 注入服務）。

## 檔案與命名

- 一個 use case 一個檔案，檔名是動詞片語：`SavePromptTemplate.cs`、`ListNotifications.cs`。同一資源、共用大量邏輯的唯讀查詢可以放在同一檔（如 `BrowseSources.cs`）。
- 模組根目錄的 `<Module>Module.cs` 建立路由群組（授權、tags、body 上限），再依序呼叫各 slice 的 `Map`。端點順序就是 OpenAPI 順序，不要任意調整。
- entity、DTO、`<Module>Errors`、查詢擴充方法與 `IEntityTypeConfiguration<T>` 放在以 entity 命名的檔案（如 `PromptTemplate.cs`）。
- slice 類別預設 `internal`；只有其他模組要用的服務（如 `NotificationService.PublishAsync`）才是 `public`，這就是模組的對外合約。

## 端點與 handler

```csharp
internal sealed class SavePromptTemplate(NexusDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", async (SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, null, body, ct)).ToHttpResult())
        .WithName("CreatePromptTemplate").Produces<PromptTemplateDto>();

    public async Task<Result<PromptTemplateDto>> HandleAsync(...) { ... }
}
```

- handler 有相依時寫成類別並在模組註冊為 scoped；只有幾行時可直接寫在 `Map` 的 lambda（注入 `NexusDbContext`）。
- 使用者一律用 `ICurrentUser`（同步 `Id`），不要在新程式呼叫 `CurrentUser.GetAsync`。
- 時間一律用注入的 `TimeProvider`，不要直接呼叫 `DateTimeOffset.UtcNow`。
- 直接使用 `NexusDbContext`；不要新增 repository 或只轉送呼叫的 service。新程式不要使用 `IEfHelper`。
- lambda 回傳 `IResult`（`Results.*` 或 `ToHttpResult()`），回應型別以 `.Produces<T>()` 宣告。

## 錯誤

- 預期內的失敗（找不到、衝突、無權限、配額）回傳 `Result<T>`／`Result`，錯誤定義在 `<Module>Errors`：`Error.NotFound("template_not_found")`。代碼必須存在於 `PublicErrorCatalog`，前後端提示由 `DiagnosticTests` 檢查一致。
- HTTP 狀態碼只由 `Problems.Status(ErrorKind)` 決定。
- 外部系統或基礎設施故障（資料庫連線、逾時）仍然丟例外，由診斷 middleware 記錄並回傳 issue code。
- `T` 是介面時用 `Result<T>.Ok(value)`（C# 不允許介面的隱含轉換）。

## 請求驗證

- request body 的格式規則寫成 `RequestValidator<T>`，與 request record 放在同一個 slice 檔。驗證 filter 已掛在 `/api/v1` 群組，handler 不必再檢查。
- 要保留模組既有的錯誤代碼時覆寫 `ProblemCode`；欄位規則代碼用 `WithErrorCode("snake_case")`。回應的 `errors` 只列欄位與規則代碼，不含輸入內容。
- 需要資料庫的規則（重複、配額、擁有權）屬於 handler，不放 validator。
- `EndpointConventionTests` 以 `request-validators.baseline.txt` 管控尚未有 validator 的 request，新增 request 必須有 validator；補上後刪掉該行。

## 授權

- policy 名稱用 `Policies.*` 或模組常數，不要寫字串。`EndpointConventionTests` 檢查每個端點都宣告授權，且每個 policy 都已註冊。

## 跨模組

- 只透過對方模組的 `public` 服務或 DTO。`module-dependencies.baseline.txt` 只能減少；新的跨模組依賴要改用合約或事件。
