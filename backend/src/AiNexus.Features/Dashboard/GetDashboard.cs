using AiNexus.Features.AccessControl;
using AiNexus.Features.Billing.Reports;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Dashboard;

public sealed record DashboardPeriodDto(DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes);

/// <summary>
/// 總覽：期間內的花費與 token 用量，加上檢視者可開啟的每個功能一區。區塊來自所有已註冊的 <see cref="IDashboardSource"/>，
/// 功能在自己的模組註冊即會出現在這裡。
/// </summary>
public sealed record DashboardDto(string Scope, Guid? OwnerId, DateTimeOffset GeneratedAt, DashboardPeriodDto Period, SpendReportDto Spend,
    TokenUsageDto Tokens, IReadOnlyList<DashboardSectionDto> Sections);

/// <summary>個人總覽；平台營運權限者可看全平台總覽，且每次查看都會留稽核。</summary>
internal sealed class GetDashboard(NexusDbContext db, SpendReports reports, UsageReports usage, AccessService access,
    IEnumerable<IDashboardSource> sources, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/dashboard", async (string? scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, GetDashboard handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, scope ?? "personal", ownerId, from, until, offsetMinutes, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Dashboard).WithName("GetDashboard");

    public async Task<Result<DashboardDto>> HandleAsync(Guid actor, string scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        if (scope is not ("personal" or "platform") || (scope == "personal" && ownerId != null)) return DashboardErrors.InvalidScope;
        var features = (await access.ForUserAsync(actor, ct)).Features.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var platform = scope == "platform";
        if (platform && !features.ContainsKey(FeatureIds.Operations)) return AccessControlErrors.FeatureForbidden;
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        Guid? owner = platform ? ownerId : actor;
        var window = new DashboardScope(actor, owner, platform, period.Value.From, period.Value.Until, period.Value.Offset);
        var sections = new List<DashboardSectionDto>();
        // 各來源共用同一個請求的 DbContext，不能並行，所以依序執行。
        foreach (var source in sources)
        {
            if (!features.TryGetValue(source.Feature, out var feature)) continue;
            if (await source.ReadAsync(window, ct) is not { } section) continue;
            sections.Add(section with { Title = section.Title.Length > 0 ? section.Title : feature.Name, Route = section.Route ?? feature.Route });
        }
        var spend = await reports.ReportAsync(owner, period.Value, platform, ct);
        var tokens = await usage.TokensAsync(owner, period.Value, platform, ct);
        if (platform)
        {
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = ownerId ?? Guid.Empty, Action = "dashboard.platform.read", Result = "metrics" });
            await db.SaveChangesAsync(ct);
        }
        return new DashboardDto(scope, owner, clock.GetUtcNow(), new(window.From, window.Until, window.OffsetMinutes), spend, tokens, sections);
    }
}
