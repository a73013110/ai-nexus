using AiNexus.BuildingBlocks;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Inference;

public sealed record RunTimingDto(long TotalMilliseconds, long QueueMilliseconds, long? GenerationMilliseconds, long? InputTokens, long? OutputTokens);

public static class RunTiming
{
    // Share one metadata-only query between owner history and audited administrative readers.
    // Message authorization belongs to the caller; answer text and generation parameters are not loaded.
    public static async Task<Dictionary<Guid, RunTimingDto>> ReadAsync(NexusDbContext db, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.Runs.AsNoTracking().Where(x => ids.Contains(x.Id) && x.DurationMilliseconds != null)
            .Select(x => new GenerationRun { Id = x.Id, DurationMilliseconds = x.DurationMilliseconds,
                GenerationMilliseconds = x.GenerationMilliseconds, InputTokens = x.InputTokens, OutputTokens = x.OutputTokens })
            .ToDictionaryAsync(x => x.Id, x => Describe(x)!, ct);
    }

    public static void Finish(GenerationRun run, DateTimeOffset now)
    {
        run.FinishedAt = now;
        run.DurationMilliseconds = Milliseconds(run.CreatedAt, now);
        run.GenerationMilliseconds = run.StartedAt is { } started ? Math.Min(run.DurationMilliseconds.Value, Milliseconds(started, now)) : null;
    }

    public static RunTimingDto? Describe(GenerationRun run) => run.DurationMilliseconds is { } total
        ? new(total, Math.Max(0, total - (run.GenerationMilliseconds ?? 0)), run.GenerationMilliseconds, run.InputTokens, run.OutputTokens) : null;

    public static long Milliseconds(DateTimeOffset start, DateTimeOffset end) => Math.Max(0, (long)(end - start).TotalMilliseconds);
}
