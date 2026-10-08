namespace AiNexus.Modules.Operations;

public static class ActivityAuditEndpoints
{
    public const string Feature = "audit", Policy = "feature:audit";

    public static void MapActivityAudit(this RouteGroupBuilder root)
    {
        // Preserve the API URL while separating its authorization from /admin management.
        var audit = root.MapGroup("/admin/audit").RequireAuthorization(Policy).WithTags("Activity audit");
        audit.MapGet("", async (long? before, string? search, string? action, string? result, DateTimeOffset? from, DateTimeOffset? until, string? category, string? traceId, ActivityAuditReader reader, CancellationToken ct) =>
            Results.Ok(await reader.QueryAsync(before, ct, search, action, result, from, until, category, traceId)))
            .WithName("ListAdminAudit").Produces<IReadOnlyList<AuditDto>>();
        audit.MapGet("/catalog", async (ActivityAuditReader reader, CancellationToken ct) => Results.Ok(await reader.CatalogAsync(ct)))
            .WithName("GetAuditCatalog").Produces<AuditCatalogDto>();
    }
}
