using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Administration;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Billing;

public static class BillingEndpoints
{
    public static void MapBilling(this RouteGroupBuilder root)
    {
        root.MapGet("/billing/spend", async (DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, CurrentUser current, SpendReports reports, CancellationToken ct) =>
            Results.Ok(await reports.ReportAsync((await current.GetAsync(ct)).Id, from, until, offsetMinutes, false, ct))).WithName("GetPersonalSpend").Produces<SpendReportDto>();
        root.MapGet("/conversations/{id:guid}/spend", async (Guid id, CurrentUser current, SpendReports reports, CancellationToken ct) =>
            Results.Ok(await reports.ConversationAsync((await current.GetAsync(ct)).Id, id, ct))).RequireAuthorization(Policies.Chat).WithName("GetConversationSpend").Produces<ConversationSpendDto>();
        var admin = root.MapGroup("/admin/billing").RequireAuthorization(AdministrationConfiguration.Policy).WithTags("Billing");
        admin.MapGet("/targets", (BillingService service) => Results.Ok(service.Targets())).WithName("ListPriceTargets").Produces<IReadOnlyList<PriceTargetDto>>();
        admin.MapGet("/prices", async (BillingService service, CancellationToken ct) => Results.Ok(await service.PricesAsync(ct))).WithName("ListModelPrices").Produces<IReadOnlyList<PriceDto>>();
        admin.MapPost("/prices", async (PriceRequest body, CurrentUser current, BillingService service, CancellationToken ct) =>
            Results.Ok(await service.AddPriceAsync((await current.GetAsync(ct)).Id, body, ct))).WithName("CreateModelPrice").Produces<PriceDto>();
        admin.MapGet("/spend", async (Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, CurrentUser current, NexusDbContext db, SpendReports reports, CancellationToken ct) =>
        {
            var report = await reports.ReportAsync(ownerId, from, until, offsetMinutes, true, ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = (await current.GetAsync(ct)).Id, Action = "billing.report.read", ResourceId = ownerId ?? Guid.Empty, Result = "read" }); await db.SaveChangesAsync(ct);
            return Results.Ok(report);
        }).WithName("GetAdministrativeSpend").Produces<SpendReportDto>();
        admin.MapGet("/export", async (Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, CurrentUser current, NexusDbContext db, SpendReports reports, CancellationToken ct) =>
        {
            var report = await reports.ReportAsync(ownerId, from, until, offsetMinutes, true, ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = (await current.GetAsync(ct)).Id, Action = "billing.report.export", ResourceId = ownerId ?? Guid.Empty, Result = "csv" }); await db.SaveChangesAsync(ct);
            return Results.File(SpendReports.Csv(report), "text/csv; charset=utf-8", "ai-nexus-spend.csv");
        }).WithName("ExportAdministrativeSpend");
    }
}
