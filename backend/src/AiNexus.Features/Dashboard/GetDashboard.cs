using AiNexus.Features.AccessControl;
using AiNexus.Features.Billing;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Dashboard;

public sealed record DashboardPeriodDto(DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes);

/// <summary>
/// The overview: spend and token usage for the period, plus one section per feature the viewer can open. Sections come
/// from every registered <see cref="IDashboardSource"/>, so a feature adds itself here from its own module.
/// </summary>
public sealed record DashboardDto(string Scope, Guid? OwnerId, DateTimeOffset GeneratedAt, DashboardPeriodDto Period, SpendReportDto Spend,
    TokenUsageDto Tokens, IReadOnlyList<DashboardSectionDto> Sections);

/// <summary>Personal overview, or the platform-wide one for administrators (audited).</summary>
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
        if (platform && !features.ContainsKey(FeatureIds.Admin)) return DashboardErrors.AccessDenied;
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        Guid? owner = platform ? ownerId : actor;
        var window = new DashboardScope(actor, owner, platform, period.Value.From, period.Value.Until, period.Value.Offset);
        var sections = new List<DashboardSectionDto>();
        // Sources share this request's context, so they run one after another.
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
