using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Dashboard;

public sealed record DashboardPeriodDto(DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes);

/// <summary>
/// 總覽：期間內的花費與 token 用量，加上檢視者可開啟的每個功能一區。區塊來自所有已註冊的 <see cref="IDashboardSource"/>，
/// 功能在自己的模組註冊即會出現在這裡。
/// </summary>
public sealed record DashboardDto(string Scope, Guid? OwnerId, DateTimeOffset GeneratedAt, DashboardPeriodDto Period, SpendReportDto Spend,
    TokenUsageDto Tokens, IReadOnlyList<DashboardSectionDto> Sections);

/// <summary>個人總覽；有平台用量頁（<see cref="FeatureCatalog.OperationsUsage"/>）授權者可看全平台總覽，且每次查看都會留稽核。</summary>
internal sealed class GetDashboard(SpendReports reports, UsageReports usage, AccessService access,
    IEnumerable<IDashboardSource> sources, TimeProvider clock, AuditTrail audit)
{
    private const string Personal = "personal", Platform = "platform";

    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/dashboard", async (string? scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, GetDashboard handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, scope ?? Personal, ownerId, from, until, offsetMinutes, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Dashboard).Audited(DashboardAudit.OverviewRead).WithName("GetDashboard");

    public async Task<Result<DashboardDto>> HandleAsync(Guid actor, string scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        // 個人總覽固定是檢視者本人，指定 ownerId 只在平台範圍有意義。
        var platform = scope == Platform;
        if (!platform && (scope != Personal || ownerId != null)) return DashboardErrors.InvalidScope;
        var features = (await access.ForUserAsync(actor, ct)).Features.ToDictionary(x => x.Id, StringComparer.Ordinal);
        if (platform && !features.ContainsKey(FeatureCatalog.OperationsUsage)) return AccessControlErrors.FeatureForbidden;
        var result = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!result.IsSuccess) return result.Error;
        var period = result.Value;
        Guid? owner = platform ? ownerId : actor;
        var window = new DashboardScope(actor, owner, platform, period.From, period.Until, period.Offset);
        var sections = new List<DashboardSectionDto>();
        // 各來源的 procedure 共用同一個請求的 DbContext 連線，不能並行，所以依序執行；每個來源一次往返。
        foreach (var source in sources)
        {
            if (!features.TryGetValue(source.Feature, out var feature)) continue;
            if (await source.ReadAsync(window, ct) is not { } section) continue;
            sections.Add(section with { Title = section.Title.Length > 0 ? section.Title : feature.Name, Route = section.Route ?? feature.Route });
        }
        var spend = await reports.ReportAsync(owner, period, platform, ct);
        var tokens = await usage.TokensAsync(owner, period, platform, ct);
        if (platform)
        {
            audit.Describe(subject: ownerId, details: new { scope });
        }
        return new DashboardDto(scope, owner, clock.GetUtcNow(), new(window.From, window.Until, window.OffsetMinutes), spend, tokens, sections);
    }
}
