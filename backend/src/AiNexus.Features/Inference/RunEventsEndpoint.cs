using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Inference;

public sealed class SubscriptionLimits
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, int> users = [];
    private int total;
    public IDisposable Acquire(Guid owner)
    {
        lock (gate)
        {
            var count = users.GetValueOrDefault(owner);
            if (count >= 2 || total >= 64) throw new ApiException(429, "subscription_limit", "事件連線數已達上限，請關閉重複的分頁後重試。");
            users[owner] = count + 1;
            total++;
        }
        return new Lease(() => { lock (gate) { if (--users[owner] == 0) users.Remove(owner); total--; } });
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}

public static class RunEventsEndpoint
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // The server-sent event stream keeps its original mapping; it resolves the user itself.
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/runs/{id:guid}/events", async (Guid id, long? after, HttpContext http, CurrentUser current, RunService service, NexusDbContext db, SubscriptionLimits limits, CancellationToken ct) =>
            await StreamAsync(http, (await current.GetAsync(ct)).Id, id, after, service, db, limits, ct)).WithName("RunEvents").Produces<RunEventDto>(200, "text/event-stream");

    public static async Task StreamAsync(HttpContext http, Guid owner, Guid id, long? after, RunService runs, NexusDbContext db, SubscriptionLimits limits, CancellationToken ct)
    {
        var run = await runs.OwnedAsync(owner, id, ct);
        var raw = http.Request.Headers["Last-Event-ID"].ToString();
        var cursor = after ?? 0;
        if (raw.Length > 0)
        {
            if (!long.TryParse(raw, out cursor)) throw new ApiException(400, "invalid_cursor", "事件序號不正確。");
        }
        if (cursor < 0 || cursor > run.LastSequence) throw new ApiException(409, "invalid_cursor", "請重新取得生成狀態，再訂閱事件。");
        using var lease = limits.Acquire(owner);
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        var heartbeat = DateTimeOffset.UtcNow;
        await WriteAsync(http, ": connected\n\n", ct);
        while (!ct.IsCancellationRequested)
        {
            var events = await db.RunEvents.AsNoTracking().Where(x => x.RunId == id && x.Sequence > cursor).OrderBy(x => x.Sequence).Take(128).ToListAsync(ct);
            run = await db.Runs.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            if ((events.Count == 0 && run.LastSequence > cursor) || (events.Count > 0 && events[0].Sequence != cursor + 1))
            {
                var snapshot = new RunEventDto(1, run.LastSequence, run.Id, "snapshot", run.Status, run.Content, run.ErrorCode, run.IssueCode);
                await SendAsync(http, snapshot, ct);
                cursor = run.LastSequence;
                events.Clear();
            }
            foreach (var item in events)
            {
                await SendAsync(http, item.ToDto(), ct);
                cursor = item.Sequence;
            }
            if (!RunStates.IsActive(run.Status) && cursor >= run.LastSequence) break;
            if (DateTimeOffset.UtcNow - heartbeat > TimeSpan.FromSeconds(10))
            {
                await WriteAsync(http, ": heartbeat\n\n", ct);
                heartbeat = DateTimeOffset.UtcNow;
            }
            await Task.Delay(150, ct);
        }
    }

    private static Task SendAsync(HttpContext http, RunEventDto value, CancellationToken ct) => WriteAsync(http, $"id: {value.Sequence}\nevent: run\ndata: {JsonSerializer.Serialize(value, Json)}\n\n", ct);
    private static async Task WriteAsync(HttpContext http, string value, CancellationToken ct)
    {
        using var slowClient = CancellationTokenSource.CreateLinkedTokenSource(ct);
        slowClient.CancelAfter(TimeSpan.FromSeconds(5));
        await http.Response.WriteAsync(value, slowClient.Token);
        await http.Response.Body.FlushAsync(slowClient.Token);
    }
}
