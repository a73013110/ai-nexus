using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Releases.SchemaChanges;

/// <summary>一個物件應有與實際的定義，給版本頁並排比對。</summary>
internal sealed class GetSchemaChange(SchemaChangeReader changes)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{schemaName}/{name}", (string schemaName, string name, GetSchemaChange handler, CancellationToken ct) =>
            handler.HandleAsync(schemaName, name, ct).ToHttpResultAsync())
        .Audited(ReleasesAudit.SchemaObjectRead)
        .WithName("GetSchemaChange");

    public async Task<Result<SchemaObjectDto>> HandleAsync(string schemaName, string name, CancellationToken ct)
        => await changes.GetAsync(schemaName, name, ct) is { } found ? found : ReleasesErrors.SchemaObjectNotFound;
}
