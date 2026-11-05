using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Http.Features;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 以頁面交來的事實回答一次提問，邊生成邊以 SSE 送出（事件見 <see cref="AssistantEventDto"/>）。
/// 模型、配額與用量走 <see cref="ModelTaskService"/>（種類 <see cref="Kind"/>），與對話同一套模型政策；回答不保存。
/// 模型、配額與上下文的錯誤都在第一段文字之前，回應還沒開始，所以是一般的錯誤回應；開始之後的失敗是 <c>error</c> 事件。
/// </summary>
[Scoped]
public sealed class AssistantResponder(ModelCatalog catalog, ModelTaskService models, AuditTrail audit)
{
    public const string Kind = "assistant";

    /// <summary>回答的輸出上限：頁內助理是短答，長篇整理交給對話。</summary>
    public const int MaxOutputTokens = 1536;

    /// <summary>
    /// 端點以 <c>ToStreamResultAsync</c> 回傳這個結果。<paramref name="grounding"/> 由頁面依自己的資料範圍算好；
    /// 稽核在第一段文字送出前定案，寫不進去就不送（與串流讀取的其他端點相同）。
    /// </summary>
    public async Task<Result> AnswerAsync(HttpContext http, Guid owner, AssistantQuestion question, AssistantGrounding grounding, CancellationToken ct)
    {
        var model = await catalog.RequireAsync(question.ModelId, ct);
        if (!model.IsSuccess) return model.Error;
        var output = Math.Min(MaxOutputTokens, model.Value.MaxOutputTokens);
        var plan = AssistantPrompt.Plan(grounding, question, model.Value.ContextTokens, output);

        var started = false;
        var generated = await models.GenerateAsync(owner, Kind, plan.Prompt, plan.Instruction, ct, question.ModelId,
            maxOutputTokens: output, history: plan.History, onText: async (text, token) =>
            {
                if (!started)
                {
                    started = true;
                    await audit.CompleteAsync(AuditEvent.Completed);
                    http.Response.ContentType = "text/event-stream";
                    // 關閉代理與 ASP.NET 的回應緩衝，否則文字會被攢住，畫面看不到逐字出現。
                    http.Response.Headers["X-Accel-Buffering"] = "no";
                    http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                    await WriteAsync(http, "grounding", new(Scope: grounding.Scope, Facts: plan.Facts, Partial: plan.Partial), token);
                }
                await WriteAsync(http, "delta", new(Text: text), token);
            });
        if (!generated.IsSuccess) return generated.Error;
        await WriteAsync(http, "done", new(Truncated: generated.Value.Truncated), ct);
        return Result.Success;
    }

    private static async Task WriteAsync(HttpContext http, string name, AssistantEventDto value, CancellationToken ct)
    {
        await http.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(value, JsonSerializerOptions.Web)}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
