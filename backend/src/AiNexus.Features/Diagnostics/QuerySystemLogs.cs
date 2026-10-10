using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Diagnostics;

/// <summary>One page of stored diagnostic events matching the filter. <see cref="DiagnosticQuery"/> validates, audits and pages.</summary>
internal sealed class QuerySystemLogs(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("", ([AsParameters] DiagnosticFilter filter, ICurrentUser user, QuerySystemLogs handler, CancellationToken ct) => handler.HandleAsync(user.Id, filter, ct).ToHttpResultAsync())
        .WithName("QuerySystemLogs");

    public Task<Result<DiagnosticPage>> HandleAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct) => query.ListAsync(actor, filter, ct);
}
