using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Billing.Reports;

/// <summary>全平台（或單一擁有者）的花費報表與匯出，給平台營運用；每次讀取與匯出都會稽核。</summary>
internal sealed class ReportAdministrativeSpend(SpendReports reports, TimeProvider clock, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("/spend", (
                Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportAdministrativeSpend handler,
                CancellationToken ct) =>
            handler.HandleAsync(ownerId, from, until, offsetMinutes, ct))
            .Audited(BillingAudit.SpendRead)
            .WithName("GetAdministrativeSpend");
        routes.MapGet("/export", (
                Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportAdministrativeSpend handler,
                CancellationToken ct) =>
            handler.HandleAsync(ownerId, from, until, offsetMinutes, ct)
                .ToHttpResultAsync(report => SpendReports.Export(report).ToHttpResult()))
            .Audited(BillingAudit.SpendExported)
            .WithName("ExportAdministrativeSpend");
    }

    public async Task<Result<SpendReportDto>> HandleAsync(Guid? owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        var report = await reports.ReportAsync(owner, period.Value, true, ct);
        audit.Describe(subject: owner);
        return report;
    }
}
