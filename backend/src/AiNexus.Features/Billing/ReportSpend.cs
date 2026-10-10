using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Billing;

/// <summary>Spend reports: personal, per conversation, and platform-wide (administrators, audited).</summary>
internal sealed class ReportSpend(NexusDbContext db, SpendReports reports, TimeProvider clock)
{
    public static void MapPersonal(RouteGroupBuilder api)
    {
        api.MapGet("/billing/spend", (DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.PersonalAsync(user.Id, from, until, offsetMinutes, ct).ToHttpResultAsync()).WithName("GetPersonalSpend");
        api.MapGet("/conversations/{id:guid}/spend", (Guid id, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.ConversationAsync(user.Id, id, ct).ToHttpResultAsync())
            .RequireAuthorization(Policies.Chat).WithName("GetConversationSpend");
    }

    public static void MapAdministrative(RouteGroupBuilder admin)
    {
        admin.MapGet("/spend", (Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.AdministrativeAsync(user.Id, ownerId, from, until, offsetMinutes, "billing.report.read", "read", ct).ToHttpResultAsync()).WithName("GetAdministrativeSpend");
        admin.MapGet("/export", (Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.AdministrativeAsync(user.Id, ownerId, from, until, offsetMinutes, "billing.report.export", "csv", ct)
                .ToHttpResultAsync(report => TypedResults.File(SpendReports.Csv(report), "text/csv; charset=utf-8", "ai-nexus-spend.csv")))
            .WithName("ExportAdministrativeSpend");
    }

    public async Task<Result<SpendReportDto>> PersonalAsync(Guid owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        return await reports.ReportAsync(owner, period.Value, false, ct);
    }

    public Task<Result<ConversationSpendDto>> ConversationAsync(Guid owner, Guid id, CancellationToken ct) => reports.ConversationAsync(owner, id, ct);

    /// <summary>A platform-wide (or one owner's) report for an administrator; every read or export is audited.</summary>
    public async Task<Result<SpendReportDto>> AdministrativeAsync(Guid actor, Guid? owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, string action, string result, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        var report = await reports.ReportAsync(owner, period.Value, true, ct);
        db.AuditEvents.Add(new AuditEvent { OwnerId = actor, Action = action, ResourceId = owner ?? Guid.Empty, Result = result });
        await db.SaveChangesAsync(ct);
        return report;
    }
}
