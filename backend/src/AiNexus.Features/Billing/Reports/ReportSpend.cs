using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Billing.Reports;

/// <summary>花費報表：個人、單一對話，以及全平台（平台營運用，每次讀取與匯出都會稽核）。</summary>
internal sealed class ReportSpend(AppDbContext db, SpendReports reports, TimeProvider clock)
{
    public static void MapPersonal(RouteGroupBuilder api)
    {
        api.MapGet("/billing/spend", (
                DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.PersonalAsync(user.Id, from, until, offsetMinutes, ct).ToHttpResultAsync())
            .WithName("GetPersonalSpend");
        api.MapGet("/conversations/{id:guid}/spend", (Guid id, ICurrentUser user, ReportSpend handler, CancellationToken ct) =>
            handler.ConversationAsync(user.Id, id, ct).ToHttpResultAsync())
            .RequireAuthorization(Policies.Chat).WithName("GetConversationSpend");
    }

    public static void MapAdministrative(RouteGroupBuilder admin)
    {
        // 報表讀取與匯出都會稽核，動作名稱不同，方便在稽核紀錄中分開查詢。
        admin.MapGet("/spend", (
                Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler,
                CancellationToken ct) =>
            handler.AdministrativeAsync(user.Id, ownerId, from, until, offsetMinutes, "billing.report.read", "read", ct)
                .ToHttpResultAsync())
            .WithName("GetAdministrativeSpend");
        admin.MapGet("/export", (
                Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportSpend handler,
                CancellationToken ct) =>
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

    public Task<Result<ConversationSpendDto>> ConversationAsync(Guid owner, Guid id, CancellationToken ct) =>
        reports.ConversationAsync(owner, id, ct);

    /// <summary>管理員讀取全平台（或單一擁有者）的報表；每次讀取或匯出都會稽核。</summary>
    public async Task<Result<SpendReportDto>> AdministrativeAsync(Guid actor, Guid? owner, DateTimeOffset? from, DateTimeOffset? until,
        int? offset, string action, string result, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        var report = await reports.ReportAsync(owner, period.Value, true, ct);
        db.AuditEvents.Add(new AuditEvent
        {
            OwnerId = actor,
            Action = action,
            ResourceId = owner ?? Guid.Empty,
            Result = result,
        });
        await db.SaveChangesAsync(ct);
        return report;
    }
}
