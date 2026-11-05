using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Http.Features;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 全站助理的提問：交給模型的工具是提問者持有功能的 <see cref="IAssistantTool"/>；在某一頁提問時，先執行描述那一頁的工具（畫面上的資料），
/// 模型需要更多時再呼叫工具（不支援工具的模型只看得到畫面資料）。邊生成邊以 SSE 送出（事件見 <see cref="AssistantEventDto"/>）。
/// 模型、配額與用量走 <see cref="ModelTaskService"/>（種類 <see cref="Kind"/>），與對話同一套模型政策；提問與回答不保存。
/// </summary>
/// <remarks>
/// 事件先攢著，到第一段文字或第一次工具呼叫（模型已經接受請求）才開始送：在那之前的模型、配額、上下文與畫面條件的錯誤都是一般的錯誤回應。
/// 每次工具呼叫各自寫一筆該工具的稽核（<see cref="IAssistantTool.Audit"/>），寫進去了才把資料交給模型。
/// </remarks>
internal sealed class AskAssistant(IEnumerable<IAssistantTool> tools, AccessService access, ModelCatalog catalog, ModelTaskService models,
    AuditTrail audit, AppDbContext db) : IEndpointSlice
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
        AssistantToolResult? view = null;
        if (pageTool is not null)
        {
            var read = await RunAsync(http, pageTool, new(viewer, AssistantArguments.Empty, question.View), ct);
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

        var stream = new EventStream(http);
        var cited = plan.Facts.Count;
        if (view is not null) stream.Add("grounding", new(Scope: view.Scope, Facts: plan.Facts, Partial: plan.Partial));

        async Task<string> InvokeAsync(InferenceToolCall call, int bytes, CancellationToken token)
        {
            if (held.FirstOrDefault(x => x.Name == call.Name) is not { } tool) return $"沒有 {call.Name} 這個工具。";
            await stream.SendAsync("tool", new(Tool: tool.Title), token);
            var (arguments, problem) = tool.Parameters.Read(call.Arguments);
            if (arguments is null) return problem!;

            // 畫面條件只給那一頁的工具：其他工具的參數名稱不同，混進去會被誤讀。
            var read = await RunAsync(http, tool, new(viewer, arguments, tool.Feature == question.Page ? question.View : null), token);
            if (!read.IsSuccess) return $"查詢失敗（{read.Error.Code}）：參數可能超出範圍或看不到這筆資料，請換條件或告訴使用者查不到。";

            var block = AssistantPrompt.Facts(tool.Title, read.Value, cited + 1, bytes);
            cited += block.Facts.Count;
            await stream.SendAsync("grounding", new(Scope: read.Value.Scope, Facts: block.Facts, Partial: block.Partial), token);
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

    /// <summary>執行一個工具；有稽核動作的先寫稽核（失敗即拒絕：寫不進去就拋出，資料不交給模型）。</summary>
    private async Task<Result<AssistantToolResult>> RunAsync(HttpContext http, IAssistantTool tool, AssistantToolCall call, CancellationToken ct)
    {
        var result = await tool.InvokeAsync(call, ct);
        if (!result.IsSuccess || tool.Audit is not { } action) return result;

        // 功能使用量依稽核的功能統計：記成工具所屬的頁面，而不是助理端點。
        http.Items[AuditContextSource.FeatureItem] = tool.Feature;
        var query = call.Arguments.IsEmpty && call.View is { } view ? Flatten(view) : call.Arguments.Values;
        audit.Record(action, call.Viewer, details: new { name = tool.Name, query });
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>畫面條件攤平成「參數名 → 文字」，與查詢字串的稽核同一種形狀（值由稽核遮罩）。</summary>
    private static Dictionary<string, string> Flatten(JsonElement view)
        => view.EnumerateObject().Where(x => x.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array))
            .ToDictionary(x => x.Name, x => x.Value.ToString(), StringComparer.Ordinal);

    /// <summary>SSE 輸出：<see cref="Add"/> 的事件攢到第一次 <see cref="SendAsync"/> 才連同表頭一起送。</summary>
    private sealed class EventStream(HttpContext http)
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
            await http.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(value, JsonSerializerOptions.Web)}\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
    }
}
