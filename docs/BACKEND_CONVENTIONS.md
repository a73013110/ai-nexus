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
- 直接使用 `NexusDbContext`；不要新增 repository 或只轉送呼叫的 service。
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

## 軟刪除

- `Conversation`、`WorkspaceResource` 在各自的 `IEntityTypeConfiguration` 宣告具名 query filter：`HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted)`（`AiNexus.Platform.Data.SoftDelete`）。一般查詢、join 與子查詢預設不含已刪除列，不必再寫 `!x.IsDeleted`。join 到這兩個 entity 的查詢也會一併濾掉，改寫 join 時要確認語意。
- 需要看到已刪除列的查詢以名稱退出：`IgnoreQueryFilters([SoftDelete.Filter])`，不要用無參數版本（會連其他 filter 一起關掉）。退出作用於**整個查詢**，同一查詢裡其他有 filter 的 entity 也會看到已刪除列，必要時自行補條件。適用：管理端稽核讀取、容器刪除時清除已刪除子項的連結、背景任務收尾（對話或文件在處理中被刪除）、清理／保留工作。
- 運算式樹內（`Where(x => ...)` 的子查詢）不能寫集合運算式，先在外面宣告 `var rows = db.Set<T>().IgnoreQueryFilters([SoftDelete.Filter]);` 再引用。
- `KnowledgeDocument`、`NexusUser.DeletedAt` 沒有 filter，仍明確寫條件：文件與其 `WorkspaceResource` 同步刪除，文件本身的 `IsDeleted` 是判斷依據；刪除的使用者保留供稽核與歷史顯示。Dapper／原生 SQL 不受 filter 影響，必須自行加條件。

## 授權

- policy 名稱用 `Policies.*` 或模組常數，不要寫字串。`EndpointConventionTests` 檢查每個端點都宣告授權，且每個 policy 都已註冊。

## 跨模組

- 只透過對方模組的 `public` 服務或 DTO。模組之間不可有循環依賴；造成循環時的處理順序見 [模組邊界](MODULE_BOUNDARIES.md)。

### 直接呼叫或 domain event

- **直接呼叫**：需要對方的回傳值、要依結果決定 HTTP 回應或是否繼續（查詢、授權、配額、排程任務、計費預約），或是對方的檢查必須在自己寫入之前完成。
- **domain event**：「A 發生後 B 要跟著處理」，A 不需要知道結果，B 的寫入要和 A 同一個交易，例如刪除對話／成果時撤銷分享、刪除專案時解除成果的專案連結。只有在依賴方向能因此反轉、避免循環時才改；如果呼叫端仍為了查詢依賴對方，改成事件沒有好處。
- 事件是發布模組裡過去式命名的 `public sealed record`，實作 `IDomainEvent`，放在引發它的 slice 檔（如 `DeleteConversation.cs` 的 `ConversationDeleted`）。訂閱模組實作 `IDomainEventHandler<T>`，在自己的 `AddServices` 以 `AddDomainEventHandler<TEvent, THandler>()` 註冊；訂閱方引用發布方，不可反過來，也不可因此形成循環。
- 發布端注入 scoped `DomainEvents`，在呼叫 `SaveChangesAsync` 前 `Raise(...)`。`NexusDbContext.SaveChangesAsync` 先分派所有待處理事件（handler 再引發的事件也會處理，最多 `DomainEvents.MaxRounds` 輪），再整理稽核列並寫入，所以 handler 新增的 entity 和稽核列與發布端一起儲存。沒有交易時會自動開一個交易包住分派與寫入。
- handler 在同一個 `NexusDbContext` 與交易內、發布端的變更寫入之前執行：可以追蹤 entity、使用 `ExecuteUpdate`／`ExecuteDelete`（立即在目前交易執行），但不可呼叫 `SaveChanges` 或自行開關交易。handler 不保證先後順序，彼此不可依賴。
- `Raise` 之後到 `SaveChangesAsync` 之間不要提早 return；分派失敗會清掉待處理事件。同步的 `SaveChanges` 遇到待處理事件會丟例外。
- 需要在交易提交後非同步處理的副作用（外部呼叫、檔案刪除）不用 domain event，交給既有的 durable job。
