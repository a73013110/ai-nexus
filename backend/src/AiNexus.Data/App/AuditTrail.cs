using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiNexus.Data.App.Audit;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Data.App;

/// <summary>
/// 稽核的唯一寫入口（ADR 0023）。宣告 <see cref="AuditedEndpoint"/> 的端點，每個請求由請求稽核 filter 開始一筆、結束時定案：
/// handler 只需以 <see cref="Describe"/> 補上對象、資料主體與明細。請求外（背景工作）用 <see cref="Record"/>。
/// </summary>
/// <remarks>
/// <para>
/// 請求開始時就把這筆稽核加進請求的 <see cref="AppDbContext"/>：handler 在自己的交易裡 SaveChanges 時，稽核與業務異動一起提交或一起回滾。
/// 從不 SaveChanges 的請求（查詢）與失敗的請求，由 <see cref="CompleteAsync"/> 以新的 DbContext 補寫，不會連帶寫入 handler 留下的未儲存異動。
/// </para>
/// <para>
/// 查詢的稽核在回應送出前寫入，寫不進去就拋出，資料不會送出（失敗即拒絕）。handler 已提交但交易之後又回滾的情況，
/// 結束時以 Id 更新、更新不到就補寫，所以失敗紀錄一定留得下來。
/// </para>
/// <para>
/// 同一人以相同條件重複查詢（畫面輪詢、重新整理）在 <see cref="RepeatedReads.Window"/> 內只記第一次：
/// 資料與條件都已留證，重複的列只會淹沒稽核。判斷只看伺服器端的身分、動作與條件，前端無法藉此略過稽核。
/// </para>
/// </remarks>
public sealed partial class AuditTrail(AppDbContext db, AuditContextSource source, IServiceScopeFactory scopes, RepeatedReads repeated)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // 明顯是自由文字的查詢參數只記「有填」：搜尋字串可能含姓名或文件內容。
    private static readonly HashSet<string> FreeText = new(StringComparer.OrdinalIgnoreCase) { "search", "q", "query", "keyword", "text", "note" };
    private const int MaxParameters = 32;

    private AuditEvent? current;
    private JsonObject? request;
    private JsonObject? details;
    private bool completed;

    /// <summary>這個請求是否有宣告稽核。</summary>
    public bool Active => current is not null;

    /// <summary>
    /// 請求稽核 filter 呼叫：以端點宣告的動作開始這個請求的稽核。路由參數 <c>id</c> 是 GUID 時預設為對象；
    /// 路由的使用者參數（<c>{userId}</c>，或 <c>users/{id}</c> 底下的 <c>id</c>）預設為資料主體，管理別人帳號的端點不必各自補。
    /// </summary>
    public void Begin(AuditAction action, Guid owner, HttpContext http)
    {
        if (current is not null) throw new InvalidOperationException("同一個請求只能開始一筆請求稽核。");
        request = RequestDetails(http);
        current = New(action, owner);
        var resource = RouteGuid(http, "id");
        current.ResourceId = resource;
        var pattern = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "";
        current.SubjectUserId = RouteGuid(http, "userId") ?? (pattern.Contains("users/{id", StringComparison.Ordinal) ? resource : null);
        current.DetailsJson = Compose();
        db.AuditEvents.Add(current);
    }

    /// <summary>
    /// 補上這個請求的稽核：<paramref name="resource"/> 是操作的資料列，<paramref name="subject"/> 是資料屬於誰（看別人的資料、改別人的帳號），
    /// <paramref name="details"/> 會與先前的明細合併（同名欄位覆蓋），鍵名必須在 <see cref="AuditRedactor"/> 白名單內。
    /// 沒有請求稽核時（宣告 NotAudited 的端點、背景工作共用同一段程式）不做任何事。
    /// </summary>
    public void Describe(Guid? resource = null, Guid? subject = null, object? details = null)
    {
        if (current is not { } target) return;
        if (resource is not null) target.ResourceId = resource;
        if (subject is not null) target.SubjectUserId = subject;
        if (details is null) return;
        this.details ??= [];
        foreach (var (key, value) in JsonSerializer.SerializeToNode(details, Json)!.AsObject().ToArray())
            this.details[key] = value?.DeepClone();
        target.DetailsJson = Compose();
    }

    /// <summary>同一個端點依輸入做不同的事時（例如有 key 是修改、沒有是新增），改用另一個宣告的動作。</summary>
    public void Use(AuditAction action)
    {
        if (current is not { } target) return;
        target.Action = action.Name;
        target.Category = AuditAction.Value(action.Category);
    }

    /// <summary>
    /// 寫一筆請求以外的稽核（背景工作、系統動作，或一個請求影響多筆資料時的額外紀錄）。只加進 ChangeTracker，
    /// 由呼叫端在自己的交易裡 SaveChanges，與異動一起提交。
    /// </summary>
    public AuditEvent Record(AuditAction action, Guid owner, Guid? resource = null, Guid? subject = null, object? details = null, string result = AuditEvent.Completed)
    {
        var entry = New(action, owner);
        entry.ResourceId = resource;
        entry.SubjectUserId = subject;
        entry.Result = result;
        entry.DetailsJson = details is null ? null : JsonSerializer.Serialize(details, Json);
        db.AuditEvents.Add(entry);
        return entry;
    }

    /// <summary>
    /// 請求結束時定案：<paramref name="result"/> 是 <see cref="AuditEvent.Completed"/> 或錯誤代碼。
    /// 寫不進去時拋出，呼叫端不可吞掉（查詢的資料因此不會送出）。
    /// </summary>
    public async Task CompleteAsync(string result)
    {
        if (current is not { } audit || completed) return;
        completed = true;
        audit.Result = result;
        var entry = db.Entry(audit);
        var persisted = entry.State is EntityState.Unchanged or EntityState.Modified;
        // handler 已在交易內提交、之後也沒有再改：成功時不必再碰資料庫。
        if (entry.State == EntityState.Unchanged && result == AuditEvent.Completed) return;
        entry.State = EntityState.Detached;
        var read = !persisted && result == AuditEvent.Completed && audit.Category == AuditAction.Value(AuditCategory.Access);
        var key = read ? $"{audit.OwnerId}|{audit.ActorId}|{audit.Action}|{audit.ResourceId}|{audit.SubjectUserId}|{request?.ToJsonString()}" : null;
        if (key is not null && repeated.Seen(key)) return;

        // 不用請求的 DbContext：它可能帶著 handler 失敗時沒存的異動，或停在已回滾的交易上。
        await using var scope = scopes.CreateAsyncScope();
        var fresh = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (persisted)
        {
            var sanitized = AuditRedactor.Sanitize(audit.DetailsJson);
            var updated = await fresh.AuditEvents.Where(x => x.Id == audit.Id).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Action, audit.Action).SetProperty(x => x.Category, audit.Category).SetProperty(x => x.Result, audit.Result)
                .SetProperty(x => x.ResourceId, audit.ResourceId).SetProperty(x => x.SubjectUserId, audit.SubjectUserId).SetProperty(x => x.DetailsJson, sanitized));
            if (updated > 0) return;
        }
        fresh.AuditEvents.Add(new AuditEvent
        {
            OwnerId = audit.OwnerId, ActorId = audit.ActorId, TraceId = audit.TraceId, OperationId = audit.OperationId, IssueCode = audit.IssueCode,
            Action = audit.Action, Category = audit.Category, FeatureId = audit.FeatureId, ResourceId = audit.ResourceId, SubjectUserId = audit.SubjectUserId,
            Result = audit.Result, DetailsJson = audit.DetailsJson, ClientAddress = audit.ClientAddress, At = audit.At,
        });
        await fresh.SaveChangesAsync();
        if (key is not null) repeated.Remember(key);
    }

    private AuditEvent New(AuditAction action, Guid owner)
    {
        var context = source.Capture(owner);
        return new AuditEvent
        {
            OwnerId = owner, ActorId = context.ActorId, TraceId = context.TraceId, OperationId = context.OperationId, FeatureId = context.FeatureId,
            ClientAddress = context.ClientAddress, Action = action.Name, Category = AuditAction.Value(action.Category), At = context.At,
        };
    }

    private string? Compose()
    {
        if (request is null && details is null) return null;
        var json = details?.DeepClone().AsObject() ?? [];
        if (request is not null) foreach (var (key, value) in request) json[key] = value?.DeepClone();
        return json.ToJsonString(Json);
    }

    /// <summary>
    /// 路由參數與查詢字串，讓查詢類稽核回答「查了什麼條件」。值只保留識別碼、日期、數字、列舉這類安全的短字串，其他一律記成 <c>[text]</c>。
    /// </summary>
    public static JsonObject? RequestDetails(HttpContext http)
    {
        var route = new JsonObject();
        foreach (var (key, value) in http.Request.RouteValues.Take(MaxParameters))
            if (ParameterName().IsMatch(key)) route[key] = Safe(key, value?.ToString());
        var query = new JsonObject();
        foreach (var (key, values) in http.Request.Query.Take(MaxParameters))
            if (ParameterName().IsMatch(key)) query[key] = values.Count == 1 ? Safe(key, values[0]) : new JsonArray([.. values.Take(MaxParameters).Select(x => (JsonNode?)Safe(key, x))]);
        if (route.Count == 0 && query.Count == 0) return null;
        var result = new JsonObject();
        if (route.Count > 0) result["route"] = route;
        if (query.Count > 0) result["query"] = query;
        return result;
    }

    private static Guid? RouteGuid(HttpContext http, string name)
        => http.Request.RouteValues.TryGetValue(name, out var value) && Guid.TryParse(value?.ToString(), out var id) ? id : null;

    private static string? Safe(string key, string? value)
        => string.IsNullOrEmpty(value) ? value : FreeText.Contains(key) || !SafeValue().IsMatch(value) ? "[text]" : value;

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.-]{0,63}$")]
    private static partial Regex ParameterName();

    [GeneratedRegex("^[A-Za-z0-9_.:+/-]{1,64}$")]
    private static partial Regex SafeValue();
}
