using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Diagnostics;

/// <summary>單一診斷事件的屬性與例外詳情，使用獨立的權限政策；讀取前會先寫稽核。</summary>
internal sealed class GetSystemLogDetail(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetSystemLogDetail handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .RequireAuthorization(DiagnosticConfiguration.DetailPolicy).WithName("GetSystemLogDetail");

    public Task<Result<DiagnosticDetail>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => query.DetailAsync(actor, id, ct);
}
