using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Inference;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Chat;

/// <summary>
/// Server-sent events of one of the user's runs after a cursor (<c>after</c> or <c>Last-Event-ID</c>), with a snapshot
/// when events were trimmed; the stream ends once the run is finished and every event was sent.
/// </summary>
internal sealed class StreamRunEvents(RunService runs, NexusDbContext db, SubscriptionLimits limits, RunSignals signals, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Wake-ups come from <see cref="RunSignals"/>; this poll only covers writes made by another process.</summary>
    private static readonly TimeSpan PollFallback = TimeSpan.FromSeconds(1);
    private const int Batch = 128;

    // A failure is answered as a problem before the stream starts; once it has started, the response is the stream.
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/runs/{id:guid}/events", async Task<Results<EmptyHttpResult, ProblemHttpResult>> (Guid id, long? after, HttpContext http, ICurrentUser user, StreamRunEvents handler, CancellationToken ct) =>
        {
            var streamed = await handler.HandleAsync(http, user.Id, id, after, ct);
            return streamed.IsSuccess ? TypedResults.Empty : streamed.Error.ToProblem();
        })
        .WithName("RunEvents").Produces<RunEventDto>(200, "text/event-stream");

    public async Task<Result> HandleAsync(HttpContext http, Guid owner, Guid id, long? after, CancellationToken ct)
    {
        var found = await runs.OwnedAsync(owner, id, ct);
        if (!found.IsSuccess) return found.Error;
        var run = found.Value;
        var raw = http.Request.Headers["Last-Event-ID"].ToString();
        var cursor = after ?? 0;
        if (raw.Length > 0 && !long.TryParse(raw, out cursor)) return ChatErrors.InvalidCursor;
        if (cursor < 0 || cursor > run.LastSequence) return ChatErrors.CursorAhead;
        using var lease = limits.TryAcquire(owner);
        if (lease is null) return ChatErrors.SubscriptionLimit;
        using var subscription = signals.Subscribe(id);
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        var heartbeat = clock.GetUtcNow();
        await WriteAsync(http, ": connected\n\n", ct);
        while (!ct.IsCancellationRequested)
        {
            // Taken before reading, so a commit between the read and the wait still wakes this loop.
            var signal = subscription.Next();
            var events = await db.RunEvents.AsNoTracking().Where(x => x.RunId == id && x.Sequence > cursor).OrderBy(x => x.Sequence).Take(Batch).ToListAsync(ct);
            var state = await db.Runs.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.LastSequence, x.Status }).SingleAsync(ct);
            var (lastSequence, status) = (state.LastSequence, state.Status);
            if ((events.Count == 0 && lastSequence > cursor) || (events.Count > 0 && events[0].Sequence != cursor + 1))
            {
                // Only a gap needs the answer text; content and sequence come from one row, so the snapshot is consistent.
                var snapshot = await db.Runs.AsNoTracking().Where(x => x.Id == id)
                    .Select(x => new RunEventDto(1, x.LastSequence, x.Id, "snapshot", x.Status, x.Content, x.ErrorCode, x.IssueCode)).SingleAsync(ct);
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
            if (!RunStates.IsActive(status) && cursor >= lastSequence) break;
            if (clock.GetUtcNow() - heartbeat > TimeSpan.FromSeconds(10))
            {
                await WriteAsync(http, ": heartbeat\n\n", ct);
                heartbeat = clock.GetUtcNow();
            }
            if (events.Count == Batch) continue;
            try { await signal.WaitAsync(PollFallback, ct); }
            catch (TimeoutException) { }
        }
        return Result.Success;
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
