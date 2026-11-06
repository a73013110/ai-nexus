using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 全站助理的提問：交給模型的工具是提問者持有功能的 <see cref="IAssistantTool"/>；在某一頁提問時，先執行描述那一頁的工具（畫面上的資料），
/// 模型需要更多時再呼叫工具（不支援工具的模型只看得到畫面資料）。邊生成邊以 SSE 送出（事件見 <see cref="AssistantEventDto"/>）。
/// 模型、配額與用量走 <see cref="ModelTaskService"/>（種類 <see cref="Kind"/>），與對話同一套模型政策；提問與回答不保存。
/// </summary>
/// <remarks>
/// 事件先攢著，到第一段文字或第一次工具呼叫（模型已經接受請求）才開始送：在那之前的模型、配額、上下文與畫面條件的錯誤都是一般的錯誤回應。
/// 每次工具呼叫各自寫一筆該工具的稽核（<see cref="IAssistantTool.Audit"/>），寫進去了才把資料交給模型。
/// 每個功能第一份結果的最前面補上提問者在該功能的資料範圍（〈規則〉），「為什麼看不到某人」不必每個工具自己寫；
/// 事實的處理頁（<see cref="AssistantLink"/>）只在提問者持有那一頁時才給網址。
/// </remarks>
internal sealed class AskAssistant(IEnumerable<IAssistantTool> tools, AccessService access, IDataScope dataScopes, ModelCatalog catalog, ModelTaskService models,
    AuditTrail audit, AppDbContext db, IOptions<JsonOptions> json) : IEndpointSlice
{
    public const string Kind = "assistant";

    /// <summary>回答的輸出上限：助理是短答，長篇整理交給對話。</summary>
    public const int MaxOutputTokens = 1536;

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/questions", (AssistantQuestion question, HttpContext http, ICurrentUser user, AskAssistant handler, CancellationToken ct)
            => handler.HandleAsync(http, user.Id, question, ct).ToStreamResultAsync(http))
        .RequireRateLimiting(AssistantModule.RateLimit).WithRequestBodyLimit(AssistantModule.BodyLimit)
        .NotAudited("讀資料的是工具：每次工具呼叫各自寫該工具的稽核（IAssistantTool.Audit）；提問與回答不保存")
        .WithName("AskAssistant").Produces<AssistantEventDto>(200, "text/event-stream");

    public async Task<Result> HandleAsync(HttpContext http, Guid viewer, AssistantQuestion question, CancellationToken ct)
    {
        var held = new List<IAssistantTool>();
        foreach (var tool in tools.OrderBy(x => x.Name, StringComparer.Ordinal))
            if (await access.HoldsAsync(viewer, tool.Feature, ct)) held.Add(tool);
        if (held.Count == 0) return AssistantErrors.Unavailable;

        var model = await catalog.RequireAsync(question.ModelId, ct);
        if (!model.IsSuccess) return model.Error;
        var output = Math.Min(MaxOutputTokens, model.Value.MaxOutputTokens);

        // 畫面資料先查：畫面條件不合法（區間顛倒等）與頁面查詢一樣是 400，在模型呼叫之前。
        var pageTool = held.FirstOrDefault(x => x.DescribesPage && x.Feature == question.Page);
        var pageName = pageTool is null ? null : FeatureCatalog.Find(pageTool.Feature)?.Name;
        var context = new RunContext(await dataScopes.ScopesAsync(viewer, ct));
        AssistantToolResult? view = null;
        if (pageTool is not null)
        {
            var read = await RunAsync(http, pageTool, new(viewer, AssistantArguments.Empty, question.View), context, ct);
            if (!read.IsSuccess) return read.Error;
            view = read.Value;
        }

        IReadOnlyList<InferenceTool> definitions = model.Value.SupportsTools
            ? [.. held.Select(x => new InferenceTool(x.Name, x.Description, x.Parameters.Schema()))]
            : [];
        var instruction = AssistantPrompt.Instruction(pageName, held.Select(x => x.Guidance), definitions.Count > 0);
        var available = model.Value.ContextTokens - output - ModelTaskService.FramingTokenReserve - AssistantPrompt.Bytes(instruction)
            - definitions.Sum(x => AssistantPrompt.Bytes(x.Name + x.Description + x.Parameters.ToJsonString()));
        var plan = AssistantPrompt.Plan(question, view, pageName, available, definitions.Count > 0);

        var stream = new EventStream(http, json.Value.SerializerOptions);
        var cited = plan.Facts.Count;
        if (view is not null) stream.Add("grounding", new(Scope: view.Scope, Facts: await SourcesAsync(viewer, plan.Facts, context, ct), Partial: plan.Partial));

        async Task<string> InvokeAsync(InferenceToolCall call, int bytes, CancellationToken token)
        {
            if (held.FirstOrDefault(x => x.Name == call.Name) is not { } tool) return $"沒有 {call.Name} 這個工具。";
            await stream.SendAsync("tool", new(Tool: tool.Title), token);
            var (arguments, problem) = tool.Parameters.Read(call.Arguments);
            if (arguments is null) return problem!;

            // 畫面條件只給那一頁的工具：其他工具的參數名稱不同，混進去會被誤讀。
            var read = await RunAsync(http, tool, new(viewer, arguments, tool.Feature == question.Page ? question.View : null), context, token);
            if (!read.IsSuccess) return $"查詢失敗（{read.Error.Code}）：參數可能超出範圍或看不到這筆資料，請換條件或告訴使用者查不到。";

            var block = AssistantPrompt.Facts(tool.Title, read.Value, cited + 1, bytes);
            cited += block.Facts.Count;
            await stream.SendAsync("grounding", new(Scope: read.Value.Scope, Facts: await SourcesAsync(viewer, block.Facts, context, token), Partial: block.Partial), token);
            return block.Text;
        }

        var generated = await models.GenerateAsync(viewer, Kind, plan.Prompt, instruction, ct, question.ModelId,
            maxOutputTokens: output, history: plan.History,
            onText: (text, token) => new(stream.SendAsync("delta", new(Text: text), token)),
            tools: definitions.Count > 0 ? new ModelTools(definitions, InvokeAsync) : null);
        if (!generated.IsSuccess) return generated.Error;
        await stream.SendAsync("done", new(Truncated: generated.Value.Truncated), ct);
        return Result.Success;
    }

    /// <summary>一次提問內跨工具呼叫共用的狀態：提問者各功能的資料範圍、已經說明過範圍的功能、持有與否已查過的處理頁。</summary>
    private sealed class RunContext(IReadOnlyDictionary<string, IReadOnlyList<string>> scopes)
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Scopes { get; } = scopes;
        public HashSet<string> Described { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> Holds { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>執行一個工具；有稽核動作的先寫稽核（失敗即拒絕：寫不進去就拋出，資料不交給模型）。</summary>
    private async Task<Result<AssistantToolResult>> RunAsync(HttpContext http, IAssistantTool tool, AssistantToolCall call, RunContext context, CancellationToken ct)
    {
        var result = await tool.InvokeAsync(call, ct);
        if (!result.IsSuccess) return result;
        if (ScopeFact(tool.Feature, context) is { } scope) result = result.Value with { Facts = [scope, .. result.Value.Facts] };
        if (tool.Audit is not { } action) return result;

        // 功能使用量依稽核的功能統計：記成工具所屬的頁面，而不是助理端點。
        http.Items[AuditContextSource.FeatureItem] = tool.Feature;
        var query = call.Arguments.IsEmpty && call.View is { } view ? Flatten(view) : call.Arguments.Values;
        audit.Record(action, call.Viewer, details: new { name = tool.Name, query });
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>功能第一次出現時的資料範圍說明；功能沒有宣告範圍（例如規則說明）或已經說明過時為 null。</summary>
    private static AssistantFact? ScopeFact(string feature, RunContext context)
    {
        if (!context.Scopes.TryGetValue(feature, out var codes) || !context.Described.Add(feature)) return null;
        var names = string.Join("、", DataScopes.InOrder(codes).Select(x => x.Name));
        return AssistantFact.Rule(AssistantPrompt.ScopeLabel,
            $"你在「{FeatureCatalog.Find(feature)?.Name ?? feature}」看得到的範圍：{names}。範圍外的資料查不到，也不會出現在這裡。");
    }

    /// <summary>送給畫面的依據：處理頁換成名稱，提問者持有那一頁才給網址（與帳號待辦的連結相同）。</summary>
    private async Task<IReadOnlyList<AssistantFactDto>> SourcesAsync(Guid viewer, IReadOnlyList<AssistantFact> facts, RunContext context, CancellationToken ct)
    {
        var sources = new List<AssistantFactDto>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.Link is not { } link || FeatureCatalog.Find(link.Feature) is not { Route: not null } page)
            {
                sources.Add(new(fact.Label, fact.Text, fact.Kind));
                continue;
            }
            if (!context.Holds.TryGetValue(page.Id, out var holds)) context.Holds[page.Id] = holds = await access.HoldsAsync(viewer, page.Id, ct);
            var url = holds ? FeatureCatalog.RouteOf(page.Id) + (link.Query is { Length: > 0 } query ? "?" + query : "") : null;
            sources.Add(new(fact.Label, fact.Text, fact.Kind, page.Name, url));
        }
        return sources;
    }

    /// <summary>畫面條件攤平成「參數名 → 文字」，與查詢字串的稽核同一種形狀（值由稽核遮罩）。</summary>
    private static Dictionary<string, string> Flatten(JsonElement view)
        => view.EnumerateObject().Where(x => x.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array))
            .ToDictionary(x => x.Name, x => x.Value.ToString(), StringComparer.Ordinal);

    /// <summary>SSE 輸出：<see cref="Add"/> 的事件攢到第一次 <see cref="SendAsync"/> 才連同表頭一起送；序列化與一般回應相同（列舉是名稱），與契約一致。</summary>
    private sealed class EventStream(HttpContext http, JsonSerializerOptions options)
    {
        private readonly List<(string Name, AssistantEventDto Data)> pending = [];
        private bool started;

        public void Add(string name, AssistantEventDto data) => pending.Add((name, data));

        public async Task SendAsync(string name, AssistantEventDto data, CancellationToken ct)
        {
            if (!started)
            {
                started = true;
                http.Response.ContentType = "text/event-stream";
                // 關閉代理與 ASP.NET 的回應緩衝，否則文字會被攢住，畫面看不到逐字出現。
                http.Response.Headers["X-Accel-Buffering"] = "no";
                http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                foreach (var (pendingName, pendingData) in pending) await WriteAsync(pendingName, pendingData, ct);
                pending.Clear();
            }
            await WriteAsync(name, data, ct);
        }

        private async Task WriteAsync(string name, AssistantEventDto value, CancellationToken ct)
        {
            await http.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(value, options)}\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
    }
}
