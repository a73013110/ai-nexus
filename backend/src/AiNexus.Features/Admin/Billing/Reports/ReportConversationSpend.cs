using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Billing.Reports;

/// <summary>使用者自己單一對話的花費，顯示在對話頁。</summary>
internal sealed class ReportConversationSpend(SpendReports reports) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/spend", (Guid id, ICurrentUser user, ReportConversationSpend handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .NotAudited("使用者自己對話的費用")
        .WithName("GetConversationSpend");

    public Task<Result<ConversationSpendDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct) => reports.ConversationAsync(owner, id, ct);
}
