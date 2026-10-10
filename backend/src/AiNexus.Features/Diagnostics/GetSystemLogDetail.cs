using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Diagnostics;

/// <summary>One diagnostic event with its properties and exception, under its own policy. The read is audited first.</summary>
internal sealed class GetSystemLogDetail(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetSystemLogDetail handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .RequireAuthorization(DiagnosticConfiguration.DetailPolicy).WithName("GetSystemLogDetail");

    public Task<Result<DiagnosticDetail>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => query.DetailAsync(actor, id, ct);
}
