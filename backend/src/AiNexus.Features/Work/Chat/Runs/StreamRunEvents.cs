using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using System.Text.Json;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Runs;

/// <summary>
/// 生成事件的 SSE 推送端點，支援從游標（after 或 Last-Event-ID）續傳。
/// 序號有跳號或舊事件已被清理時改送完整 snapshot；每次寫入限時 5 秒，避免慢客戶端長期占住連線；
/// 每 10 秒送一次心跳，防止中介 Proxy 把閒置連線切斷。
/// </summary>
internal sealed class StreamRunEvents(
    RunService runs,
    AppDbContext db,
    SubscriptionLimits limits,
    RunSignals signals,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>信號喚醒為主；若為多行程部署，以 1 秒輪詢作為後備跨行程喚醒。</summary>
    private static readonly TimeSpan PollFallback = TimeSpan.FromSeconds(1);
    private const int Batch = 128;

    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/runs/{id:guid}/events", (Guid id, long? after, HttpContext http, ICurrentUser user, StreamRunEvents handler, CancellationToken ct) =>
            handler.HandleAsync(http, user.Id, id, after, ct).ToStreamResultAsync(http))
        .NotAudited("生成串流，送出訊息已留證")
        .WithName("RunEvents")
        .Produces<RunEventDto>(200, "text/event-stream");

    public async Task<Result> HandleAsync(HttpContext http, Guid owner, Guid id, long? after, CancellationToken ct)
    {
        var found = await runs.OwnedAsync(owner, id, ct);
        if (!found.IsSuccess) return found.Error;
        var run = found.Value;

        // 解析重連游標（優先採用 Last-Event-ID 標頭）
        var raw = http.Request.Headers["Last-Event-ID"].ToString();
        var cursor = after ?? 0;
        if (raw.Length > 0 && !long.TryParse(raw, out cursor))
        {
            return ChatErrors.InvalidCursor;
        }

        // 游標不得為負數，也不能超過資料庫中目前的最新序號，否則視為無效
        if (cursor < 0 || cursor > run.LastSequence)
        {
            return ChatErrors.CursorAhead;
        }

        using var lease = limits.TryAcquire(owner);
        if (lease is null)
        {
            return ChatErrors.SubscriptionLimit;
        }

        using var subscription = signals.Subscribe(id);

        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var heartbeat = clock.GetUtcNow();
        await WriteAsync(http, ": connected\n\n", ct);

        while (!ct.IsCancellationRequested)
        {
            // 在查詢資料庫前先取得信號 Task，避免查詢後與等待前的空檔漏失更新
            var signal = subscription.Next();

            var events = await db.RunEvents
                .AsNoTracking()
                .Where(x => x.RunId == id && x.Sequence > cursor)
                .OrderBy(x => x.Sequence)
                .Take(Batch)
                .ToListAsync(ct);

            var state = await db.Runs
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.LastSequence, x.Status })
                .SingleAsync(ct);

            var (lastSequence, status) = (state.LastSequence, state.Status);

            // 空洞檢查：若資料庫已有新序號但找不到增量事件，或事件序號不連續，說明早期事件已保留清理
            // 此時改為發送完整文字快照（snapshot）以平滑重連
            if ((events.Count == 0 && lastSequence > cursor) ||
                (events.Count > 0 && events[0].Sequence != cursor + 1))
            {
                var snapshot = await db.Runs
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new RunEventDto(1, x.LastSequence, x.Id, "snapshot", x.Status, x.Content, x.ErrorCode, x.IssueCode))
                    .SingleAsync(ct);

                await SendAsync(http, snapshot, ct);
                cursor = lastSequence = snapshot.Sequence;
                status = snapshot.Status;
                events.Clear();
            }

            foreach (var item in events)
            {
                await SendAsync(http, item.ToDto(), ct);
                cursor = item.Sequence;
            }

            // 先送完事件再判斷結束，確保最後一則不會因為狀態已結束而漏送
            if (!RunStates.IsActive(status) && cursor >= lastSequence)
            {
                break;
            }

            if (clock.GetUtcNow() - heartbeat > TimeSpan.FromSeconds(10))
            {
                await WriteAsync(http, ": heartbeat\n\n", ct);
                heartbeat = clock.GetUtcNow();
            }

            // 整批取滿代表後面還有資料，直接再查，不等待信號
            if (events.Count == Batch) continue;

            try
            {
                await signal.WaitAsync(PollFallback, ct);
            }
            catch (TimeoutException) { }
        }

        return Result.Success;
    }

    private static Task SendAsync(HttpContext http, RunEventDto value, CancellationToken ct) =>
        WriteAsync(http, $"id: {value.Sequence}\nevent: run\ndata: {JsonSerializer.Serialize(value, Json)}\n\n", ct);

    private static async Task WriteAsync(HttpContext http, string value, CancellationToken ct)
    {
        using var slowClient = CancellationTokenSource.CreateLinkedTokenSource(ct);
        slowClient.CancelAfter(TimeSpan.FromSeconds(5));
        await http.Response.WriteAsync(value, slowClient.Token);
        await http.Response.Body.FlushAsync(slowClient.Token);
    }
}
