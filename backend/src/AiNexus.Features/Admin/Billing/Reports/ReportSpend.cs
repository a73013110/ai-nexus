using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Billing.Reports;

/// <summary>花費報表：個人、單一對話，以及全平台（平台營運用，每次讀取與匯出都會稽核）。</summary>
internal sealed class ReportSpend(SpendReports reports, TimeProvider clock, AuditTrail audit)
{
    public static void MapPersonal(RouteGroupBuilder api)
    {
        api.MapGet("/billing/spend", (
                DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.PersonalAsync(user.Id, from, until, offsetMinutes, ct).ToHttpResultAsync())
            .NotAudited("使用者自己的費用")
            .WithName("GetPersonalSpend");
        api.MapGet("/conversations/{id:guid}/spend", (Guid id, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.ConversationAsync(user.Id, id, ct).ToHttpResultAsync())
            .RequireAuthorization(Policies.Chat).NotAudited("使用者自己對話的費用").WithName("GetConversationSpend");
    }

    public static void MapAdministrative(RouteGroupBuilder admin)
    {
        admin.MapGet("/spend", (
                Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler,
                CancellationToken ct) =>
            handler.AdministrativeAsync(ownerId, from, until, offsetMinutes, ct)
                .ToHttpResultAsync())
            .Audited(BillingAudit.SpendRead)
            .WithName("GetAdministrativeSpend");
        admin.MapGet("/export", (
                Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler,
                CancellationToken ct) =>
            handler.AdministrativeAsync(ownerId, from, until, offsetMinutes, ct)
                .ToHttpResultAsync(report => SpendReports.Export(report).ToHttpResult()))
            .Audited(BillingAudit.SpendExported)
            .WithName("ExportAdministrativeSpend");
    }

    public async Task<Result<SpendReportDto>> PersonalAsync(Guid owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        return await reports.ReportAsync(owner, period.Value, false, ct);
    }

    public Task<Result<ConversationSpendDto>> ConversationAsync(Guid owner, Guid id, CancellationToken ct) =>
        reports.ConversationAsync(owner, id, ct);

    /// <summary>管理員讀取全平台（或單一擁有者）的報表；每次讀取或匯出都會稽核。</summary>
    public async Task<Result<SpendReportDto>> AdministrativeAsync(Guid? owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        var report = await reports.ReportAsync(owner, period.Value, true, ct);
        audit.Describe(subject: owner);
        return report;
    }
}
