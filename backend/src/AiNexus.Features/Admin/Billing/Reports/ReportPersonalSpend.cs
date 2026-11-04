using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Billing.Reports;

/// <summary>使用者自己的花費報表；只看自己的資料，登入即可。</summary>
internal sealed class ReportPersonalSpend(SpendReports reports, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/billing/spend", (
                DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, ReportPersonalSpend handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, from, until, offsetMinutes, ct))
        .NotAudited("使用者自己的費用")
        .WithName("GetPersonalSpend");

    public async Task<Result<SpendReportDto>> HandleAsync(Guid owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        return await reports.ReportAsync(owner, period.Value, false, ct);
    }
}
