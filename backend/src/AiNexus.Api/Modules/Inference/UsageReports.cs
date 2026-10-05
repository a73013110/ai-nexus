using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Inference;

public sealed record UsageTotalsDto(int Requests, int Completed, int Failed, int Cancelled, long InputTokens, long OutputTokens, int RequestsWithUsage, long TotalDurationMilliseconds = 0, int TimedRequests = 0);
public sealed record UsageKindDto(string Kind, int Requests, long InputTokens, long OutputTokens);
public sealed record OwnerUsageDto(Guid OwnerId, UsageTotalsDto Usage);

/// <summary>One accounting query for personal and authorized administrative reports. Callers own authorization.</summary>
public sealed class UsageReports(NexusDbContext db, AttachmentQuota quota)
{
    public static DateTimeOffset Since => DateTimeOffset.UtcNow.Date.AddDays(-29);
    private sealed class Entry
    {
        public Guid OwnerId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string Kind { get; init; } = "";
        public string Status { get; init; } = "";
        public long? InputTokens { get; init; }
        public long? OutputTokens { get; init; }
        public long? DurationMilliseconds { get; init; }
    }
    private IQueryable<Entry> Entries() => db.Runs.AsNoTracking().Where(x => x.CreatedAt >= Since)
        .Select(x => new Entry { OwnerId = x.OwnerId, CreatedAt = x.CreatedAt, Kind = "chat", Status = x.Status, InputTokens = x.InputTokens, OutputTokens = x.OutputTokens, DurationMilliseconds = x.DurationMilliseconds })
        .Concat(db.Set<ModelInvocation>().AsNoTracking().Where(x => x.CreatedAt >= Since)
            .Select(x => new Entry { OwnerId = x.OwnerId, CreatedAt = x.CreatedAt, Kind = x.Kind, Status = x.Status, InputTokens = x.InputTokens, OutputTokens = x.OutputTokens, DurationMilliseconds = x.DurationMilliseconds }));
    public async Task<IReadOnlyList<OwnerUsageDto>> ByOwnersAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await Entries().Where(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId)
            .Select(g => new OwnerUsageDto(g.Key, new UsageTotalsDto(g.Count(), g.Count(x => x.Status == "completed"), g.Count(x => x.Status == "failed"), g.Count(x => x.Status == "cancelled"),
                g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0), g.Count(x => x.InputTokens.HasValue && x.OutputTokens.HasValue), g.Sum(x => x.DurationMilliseconds ?? 0), g.Count(x => x.DurationMilliseconds.HasValue)))).ToListAsync(ct);
    public async Task<UsageTotalsDto> AllAsync(CancellationToken ct) => await Entries().GroupBy(x => 1)
        .Select(g => new UsageTotalsDto(g.Count(), g.Count(x => x.Status == "completed"), g.Count(x => x.Status == "failed"), g.Count(x => x.Status == "cancelled"),
            g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0), g.Count(x => x.InputTokens.HasValue && x.OutputTokens.HasValue), g.Sum(x => x.DurationMilliseconds ?? 0), g.Count(x => x.DurationMilliseconds.HasValue))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0, 0, 0);
    public async Task<PersonalUsageDto> ForOwnerAsync(Guid owner, CancellationToken ct)
    {
        var query = Entries().Where(x => x.OwnerId == owner);
        // SQL aggregates dates; SQLite's test-only offset converter requires grouping its tiny fixtures in memory.
        IReadOnlyList<UsageDayDto> daily;
        if (db.Database.IsSqlServer()) daily = await query.GroupBy(x => x.CreatedAt.Date).OrderBy(g => g.Key)
            .Select(g => new UsageDayDto(DateOnly.FromDateTime(g.Key), g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToListAsync(ct);
        else daily = (await query.ToListAsync(ct)).GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.UtcDateTime)).OrderBy(g => g.Key)
            .Select(g => new UsageDayDto(g.Key, g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToArray();
        var totals = (await ByOwnersAsync([owner], ct)).FirstOrDefault()?.Usage ?? new(0, 0, 0, 0, 0, 0, 0);
        var storage = await quota.ForAsync(owner, ct);
        return new(30, totals.Requests, totals.Completed, totals.Failed, totals.Cancelled, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage, daily, storage, totals.TotalDurationMilliseconds, totals.TimedRequests);
    }
    public async Task<IReadOnlyList<UsageKindDto>> KindsAsync(Guid owner, CancellationToken ct) => await Entries().Where(x => x.OwnerId == owner)
        .GroupBy(x => x.Kind).OrderBy(g => g.Key).Select(g => new UsageKindDto(g.Key, g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToListAsync(ct);
}
