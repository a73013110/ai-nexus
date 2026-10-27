using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Audit;

/// <summary>一個頁面功能在期間內的用量：成功操作的人數與次數、失敗（含授權拒絕）次數與最後使用時間。</summary>
public sealed record FeatureUsageDto(string FeatureId, int Users, int Events, int Failures, DateTimeOffset? LastUsedAt);

public sealed record FeatureUsageReportDto(DateTimeOffset From, DateTimeOffset Until, IReadOnlyList<FeatureUsageDto> Features);

public sealed record FeatureUsageDayDto(DateOnly Date, int Users, int Events);

public sealed record FeatureUsageUserDto(Guid UserId, string Account, string DisplayName, int Events, DateTimeOffset LastUsedAt);

/// <summary>單一頁面功能的每日人數與次數（依 <c>Offset</c> 分鐘的時區分日），以及使用最多的人。</summary>
public sealed record FeatureUsageDetailDto(string FeatureId, DateTimeOffset From, DateTimeOffset Until, int Offset,
    IReadOnlyList<FeatureUsageDayDto> Days, IReadOnlyList<FeatureUsageUserDto> Users);

/// <summary>
/// 功能使用量：由稽核紀錄的 <c>FeatureId</c>（通過授權的功能）統計，所以只算宣告稽核的端點，未稽核的輪詢與選單不灌水。
/// 動作功能（例如 <c>attendance.cards.maintain</c>）併入所屬頁面；使用者是實際操作者，測試身分算在管理者身上。
/// 範圍與活動稽核相同的規則（<see cref="AuditDataScope"/>），但另外授予 <see cref="FeatureCatalog.AuditUsage"/>。
/// </summary>
internal sealed class ReportFeatureUsage(AppDbContext db, AuditDataScope scope, TimeProvider clock)
{
    private const int TopUsers = 20;

    public static void Map(RouteGroupBuilder usage)
    {
        usage.MapGet("", (DateTimeOffset? from, DateTimeOffset? until, ICurrentUser user, ReportFeatureUsage handler, CancellationToken ct) =>
                handler.ListAsync(user.Id, from, until, ct).ToHttpResultAsync())
            .Audited(AuditAudit.UsageRead)
            .WithName("ListFeatureUsage");
        usage.MapGet("/{featureId}", (string featureId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportFeatureUsage handler,
                CancellationToken ct) => handler.DetailAsync(user.Id, featureId, from, until, offsetMinutes, ct).ToHttpResultAsync())
            .Audited(AuditAudit.UsageRead)
            .WithName("GetFeatureUsage");
    }

    public async Task<Result<FeatureUsageReportDto>> ListAsync(Guid actor, DateTimeOffset? from, DateTimeOffset? until, CancellationToken ct)
    {
        if (Period(from, until) is not var (start, end)) return AuditErrors.InvalidFilter;
        var events = await EventsAsync(actor, start, end, ct);
        // 先在 SQL 依（功能, 使用者）彙總，再於記憶體併入頁面：同一人用了頁面與它的動作只算一個人。
        var used = await events.Where(x => x.Result == AuditEvent.Completed)
            .GroupBy(x => new { x.FeatureId, User = x.ActorId ?? x.OwnerId })
            .Select(g => new { g.Key.FeatureId, g.Key.User, Events = g.Count(), Last = g.Max(x => x.At) })
            .ToListAsync(ct);
        var failed = await events.Where(x => x.Result != AuditEvent.Completed)
            .GroupBy(x => x.FeatureId).Select(g => new { FeatureId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var pages = used.Select(x => PageOf(x.FeatureId!)).Concat(failed.Select(x => PageOf(x.FeatureId!))).Distinct();
        var rows = pages.Select(page =>
        {
            var mine = used.Where(x => PageOf(x.FeatureId!) == page).ToList();
            return new FeatureUsageDto(page, mine.Select(x => x.User).Distinct().Count(), mine.Sum(x => x.Events),
                failed.Where(x => PageOf(x.FeatureId!) == page).Sum(x => x.Count), mine.Count == 0 ? null : mine.Max(x => x.Last));
        }).OrderByDescending(x => x.Users).ThenByDescending(x => x.Events).ThenBy(x => x.FeatureId, StringComparer.Ordinal).ToArray();
        return new FeatureUsageReportDto(start, end, rows);
    }

    public async Task<Result<FeatureUsageDetailDto>> DetailAsync(Guid actor, string featureId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes,
        CancellationToken ct)
    {
        if (Period(from, until) is not var (start, end) || offsetMinutes is < -840 or > 840) return AuditErrors.InvalidFilter;
        var feature = FeatureCatalog.All.FirstOrDefault(x => x.Id == featureId && x.Kind == FeatureKind.Page);
        if (feature is null) return AuditErrors.FeatureNotFound;
        var offset = offsetMinutes ?? 0;
        var ids = FeatureCatalog.All.Where(x => PageOf(x.Id) == feature.Id).Select(x => x.Id).ToArray();
        var events = (await EventsAsync(actor, start, end, ct)).Where(x => ids.Contains(x.FeatureId!) && x.Result == AuditEvent.Completed);
        var days = await events
            .GroupBy(x => x.At.AddMinutes(offset).Date)
            .Select(g => new { Date = g.Key, Users = g.Select(x => x.ActorId ?? x.OwnerId).Distinct().Count(), Events = g.Count() })
            .OrderBy(x => x.Date).ToListAsync(ct);
        var top = await events
            .GroupBy(x => x.ActorId ?? x.OwnerId)
            .Select(g => new { User = g.Key, Events = g.Count(), Last = g.Max(x => x.At) })
            .OrderByDescending(x => x.Events).ThenByDescending(x => x.Last).Take(TopUsers)
            .Join(db.Users, x => x.User, user => user.Id, (x, user) => new { x.User, x.Events, x.Last, user.AdAccount, user.LocalAccount, user.DisplayName })
            .ToListAsync(ct);
        return new FeatureUsageDetailDto(feature.Id, start, end, offset,
            [.. days.Select(x => new FeatureUsageDayDto(DateOnly.FromDateTime(x.Date), x.Users, x.Events))],
            [.. top.OrderByDescending(x => x.Events).ThenByDescending(x => x.Last)
                .Select(x => new FeatureUsageUserDto(x.User, x.AdAccount ?? x.LocalAccount ?? "", x.DisplayName, x.Events, x.Last))]);
    }

    /// <summary>期間內、有授權功能、屬於範圍內使用者的稽核紀錄（系統動作沒有功能，不列入）。</summary>
    private async Task<IQueryable<AuditEvent>> EventsAsync(Guid actor, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
    {
        var people = await scope.PeopleAsync(actor, FeatureCatalog.AuditUsage, ct);
        var query = db.AuditEvents.AsNoTracking().Where(x => x.FeatureId != null && x.At >= from && x.At < until && x.OwnerId != Guid.Empty);
        return people is null ? query : query.Where(x => people.Contains(x.ActorId ?? x.OwnerId));
    }

    /// <summary>未指定時是最近 30 天；最長一年。</summary>
    private (DateTimeOffset From, DateTimeOffset Until)? Period(DateTimeOffset? from, DateTimeOffset? until)
    {
        var end = until ?? clock.GetUtcNow();
        var start = from ?? end.AddDays(-30);
        return start >= end || end - start > TimeSpan.FromDays(366) ? null : (start.ToUniversalTime(), end.ToUniversalTime());
    }

    /// <summary>動作功能併入所屬頁面（去掉最後一段後是頁面的話）；其他維持原功能。</summary>
    internal static string PageOf(string featureId)
    {
        if (FeatureCatalog.All.FirstOrDefault(x => x.Id == featureId) is not { Kind: FeatureKind.Action }) return featureId;
        var parent = featureId[..Math.Max(featureId.LastIndexOf('.'), 0)];
        return FeatureCatalog.All.Any(x => x.Id == parent && x.Kind == FeatureKind.Page) ? parent : featureId;
    }
}
