namespace AiNexus.Features.Diagnostics;

/// <summary>One diagnostic event with its properties and exception, under its own policy. The read is audited first.</summary>
internal static class GetSystemLogDetail
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/{id:guid}", async (Guid id, DiagnosticQuery query, CancellationToken ct) => Results.Ok(await query.DetailAsync(id, ct)))
        .RequireAuthorization(DiagnosticConfiguration.DetailPolicy).WithName("GetSystemLogDetail").Produces<DiagnosticDetail>();
}
