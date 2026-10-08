using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Billing;

/// <summary>Spend reports: personal, per conversation, and platform-wide (administrators, audited).</summary>
internal static class ReportSpend
{
    public static void MapPersonal(RouteGroupBuilder api)
    {
        api.MapGet("/billing/spend", async (DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, SpendReports reports, TimeProvider clock, CancellationToken ct) =>
        {
            var period = SpendPeriod.Create(from, until, offsetMinutes, clock.GetUtcNow());
            return period.IsSuccess ? Results.Ok(await reports.ReportAsync(user.Id, period.Value, false, ct)) : period.Error.ToProblem();
        }).WithName("GetPersonalSpend").Produces<SpendReportDto>();
        api.MapGet("/conversations/{id:guid}/spend", async (Guid id, ICurrentUser user, SpendReports reports, CancellationToken ct) =>
            (await reports.ConversationAsync(user.Id, id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Chat).WithName("GetConversationSpend").Produces<ConversationSpendDto>();
    }

    public static void MapAdministrative(RouteGroupBuilder admin)
    {
        admin.MapGet("/spend", async (Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, NexusDbContext db, SpendReports reports, TimeProvider clock, CancellationToken ct) =>
        {
            var period = SpendPeriod.Create(from, until, offsetMinutes, clock.GetUtcNow());
            if (!period.IsSuccess) return period.Error.ToProblem();
            var report = await reports.ReportAsync(ownerId, period.Value, true, ct);
            await AuditAsync(db, user.Id, "billing.report.read", ownerId, "read", ct);
            return Results.Ok(report);
        }).WithName("GetAdministrativeSpend").Produces<SpendReportDto>();
        admin.MapGet("/export", async (Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, NexusDbContext db, SpendReports reports, TimeProvider clock, CancellationToken ct) =>
        {
            var period = SpendPeriod.Create(from, until, offsetMinutes, clock.GetUtcNow());
            if (!period.IsSuccess) return period.Error.ToProblem();
            var report = await reports.ReportAsync(ownerId, period.Value, true, ct);
            await AuditAsync(db, user.Id, "billing.report.export", ownerId, "csv", ct);
            return Results.File(SpendReports.Csv(report), "text/csv; charset=utf-8", "ai-nexus-spend.csv");
        }).WithName("ExportAdministrativeSpend");
    }

    private static Task AuditAsync(NexusDbContext db, Guid actor, string action, Guid? owner, string result, CancellationToken ct)
    {
        db.AuditEvents.Add(new AuditEvent { OwnerId = actor, Action = action, ResourceId = owner ?? Guid.Empty, Result = result });
        return db.SaveChangesAsync(ct);
    }
}
