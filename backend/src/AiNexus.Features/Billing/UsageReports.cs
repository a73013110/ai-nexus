using System.Globalization;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Billing;

public sealed record UsageTotalsDto(int Requests, int Completed, int Failed, int Cancelled, long InputTokens, long OutputTokens, int RequestsWithUsage, long TotalDurationMilliseconds = 0, int TimedRequests = 0);
public sealed record UsageKindDto(string Kind, int Requests, long InputTokens, long OutputTokens);
public sealed record UsageModelDto(string ModelId, int Requests, int Failed, int RequestsWithUsage, long InputTokens, long OutputTokens, long DurationMilliseconds, string? ModelDisplayName = null);
public sealed record OwnerUsageDto(Guid OwnerId, UsageTotalsDto Usage);
public sealed record TokenDayDto(string Date, string ModelId, int Requests, int RequestsWithUsage, long InputTokens, long OutputTokens, string? ModelDisplayName = null);
public sealed record UsageDayDto(DateOnly Date, int Requests, long InputTokens, long OutputTokens);
public sealed record TokenUsageDto(DateTimeOffset From, DateTimeOffset Until, int TimezoneOffsetMinutes, IReadOnlyList<TokenDayDto> Daily);

/// <summary>One accounting query for personal and authorized administrative reports. Callers own authorization.</summary>
public sealed class UsageReports(NexusDbContext db, ModelPresentation presentation, TimeProvider clock)
{
    public DateTimeOffset Since => UtcDay.Start(clock.GetUtcNow()).AddDays(-29);
    private sealed class Entry
    {
        public Guid OwnerId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string Kind { get; init; } = "";
        public string ModelId { get; init; } = "";
        public string Status { get; init; } = "";
        public long? InputTokens { get; init; }
        public long? OutputTokens { get; init; }
        public long? DurationMilliseconds { get; init; }
    }
    private IQueryable<Entry> Entries(DateTimeOffset? from = null, DateTimeOffset? until = null) => db.Runs.AsNoTracking().Where(x => x.CreatedAt >= (from ?? Since) && (until == null || x.CreatedAt < until))
        .Select(x => new Entry { OwnerId = x.OwnerId, CreatedAt = x.CreatedAt, Kind = "chat", ModelId = x.ModelId, Status = x.Status, InputTokens = x.InputTokens, OutputTokens = x.OutputTokens, DurationMilliseconds = x.DurationMilliseconds })
        .Concat(db.Set<ModelInvocation>().AsNoTracking().Where(x => x.CreatedAt >= (from ?? Since) && (until == null || x.CreatedAt < until))
            .Select(x => new Entry { OwnerId = x.OwnerId, CreatedAt = x.CreatedAt, Kind = x.Kind, ModelId = x.ModelId, Status = x.Status, InputTokens = x.InputTokens, OutputTokens = x.OutputTokens, DurationMilliseconds = x.DurationMilliseconds }));
    public async Task<IReadOnlyList<OwnerUsageDto>> ByOwnersAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await Entries().Where(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId)
            .Select(g => new OwnerUsageDto(g.Key, new UsageTotalsDto(g.Count(), g.Count(x => x.Status == "completed"), g.Count(x => x.Status == "failed"), g.Count(x => x.Status == "cancelled"),
                g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0), g.Count(x => x.InputTokens.HasValue && x.OutputTokens.HasValue), g.Sum(x => x.DurationMilliseconds ?? 0), g.Count(x => x.DurationMilliseconds.HasValue)))).ToListAsync(ct);
    public async Task<UsageTotalsDto> AllAsync(CancellationToken ct) => await Entries().GroupBy(x => 1)
        .Select(g => new UsageTotalsDto(g.Count(), g.Count(x => x.Status == "completed"), g.Count(x => x.Status == "failed"), g.Count(x => x.Status == "cancelled"),
            g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0), g.Count(x => x.InputTokens.HasValue && x.OutputTokens.HasValue), g.Sum(x => x.DurationMilliseconds ?? 0), g.Count(x => x.DurationMilliseconds.HasValue))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0, 0, 0);
    public Task<int> ActiveOwnersAsync(CancellationToken ct) => Entries().Select(x => x.OwnerId).Distinct().CountAsync(ct);
    public async Task<IReadOnlyList<UsageModelDto>> ModelsAsync(CancellationToken ct) => (await Entries().GroupBy(x => x.ModelId).OrderBy(g => g.Key)
        .Select(g => new UsageModelDto(g.Key, g.Count(), g.Count(x => x.Status == "failed"), g.Count(x => x.InputTokens != null && x.OutputTokens != null),
            g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0), g.Sum(x => x.DurationMilliseconds ?? 0), null)).ToListAsync(ct))
        .Select(x => x with { ModelDisplayName = presentation.DisplayName(x.ModelId, administrator: true) }).ToArray();
    public async Task<IReadOnlyList<UsageKindDto>> PlatformKindsAsync(CancellationToken ct) => await Entries().GroupBy(x => x.Kind).OrderBy(g => g.Key)
        .Select(g => new UsageKindDto(g.Key, g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToListAsync(ct);
    public async Task<IReadOnlyList<UsageDayDto>> DailyAsync(Guid owner, CancellationToken ct)
    {
        var query = Entries().Where(x => x.OwnerId == owner);
        // SQL aggregates dates; SQLite's test-only offset converter requires grouping its tiny fixtures in memory.
        if (db.Database.IsSqlServer()) return await query.GroupBy(x => x.CreatedAt.Date).OrderBy(g => g.Key)
            .Select(g => new UsageDayDto(DateOnly.FromDateTime(g.Key), g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToListAsync(ct);
        return (await query.ToListAsync(ct)).GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.UtcDateTime)).OrderBy(g => g.Key)
            .Select(g => new UsageDayDto(g.Key, g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToArray();
    }
    public async Task<TokenUsageDto> TokensAsync(Guid? owner, SpendPeriod period, bool administrator, CancellationToken ct)
    {
        var query = Entries(period.From, period.Until).Where(x => owner == null || x.OwnerId == owner);
        IReadOnlyList<TokenDayDto> rows;
        if (db.Database.IsSqlServer()) rows = (await query.GroupBy(x => new { Day = x.CreatedAt.AddMinutes(period.Offset).Date, x.ModelId })
            .Select(g => new { g.Key.Day, g.Key.ModelId, Requests = g.Count(), Known = g.Count(x => x.InputTokens != null && x.OutputTokens != null), Input = g.Sum(x => x.InputTokens ?? 0), Output = g.Sum(x => x.OutputTokens ?? 0) }).ToListAsync(ct))
            .Select(x => new TokenDayDto(x.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.ModelId, x.Requests, x.Known, x.Input, x.Output)).ToArray();
        else rows = (await query.ToListAsync(ct)).GroupBy(x => new { Day = x.CreatedAt.ToOffset(TimeSpan.FromMinutes(period.Offset)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.ModelId })
            .Select(g => new TokenDayDto(g.Key.Day, g.Key.ModelId, g.Count(), g.Count(x => x.InputTokens != null && x.OutputTokens != null), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToArray();
        // Merge aliases after presentation so hidden model identifiers never enter personal reports.
        var presented = rows.GroupBy(x => new { x.Date, Model = administrator ? x.ModelId : presentation.PublicId(x.ModelId) })
            .Select(g => new TokenDayDto(g.Key.Date, g.Key.Model, g.Sum(x => x.Requests), g.Sum(x => x.RequestsWithUsage), g.Sum(x => x.InputTokens), g.Sum(x => x.OutputTokens), presentation.DisplayName(g.First().ModelId, administrator)))
            .OrderBy(x => x.Date).ThenBy(x => x.ModelId).ToArray();
        return new(period.From, period.Until, period.Offset, presented);
    }
    public async Task<IReadOnlyList<UsageKindDto>> KindsAsync(Guid owner, CancellationToken ct) => await Entries().Where(x => x.OwnerId == owner)
        .GroupBy(x => x.Kind).OrderBy(g => g.Key).Select(g => new UsageKindDto(g.Key, g.Count(), g.Sum(x => x.InputTokens ?? 0), g.Sum(x => x.OutputTokens ?? 0))).ToListAsync(ct);
}
