using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>
/// Downloads one version as Word, PDF or Markdown. Read access is checked again after rendering, so access revoked
/// while a slow PDF renders is not bypassed; the file is never cached.
/// </summary>
internal sealed class ExportArtifact(GetArtifact reader, ArtifactExport exporter)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/export/{format}", (Guid id, string format, int? version, ICurrentUser user, ExportArtifact handler, HttpContext http, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, format, version, ct).ToHttpResultAsync(exported =>
            {
                http.Response.Headers.CacheControl = "private, no-store";
                return TypedResults.File(exported.File.Data, exported.File.ContentType, exported.FileName);
            }))
        .WithName("ExportArtifact");

    internal sealed record ExportedArtifact(string FileName, ExportFile File);

    public async Task<Result<ExportedArtifact>> HandleAsync(Guid actor, Guid id, string format, int? version, CancellationToken ct)
    {
        var artifact = await reader.HandleAsync(actor, id, version, ct);
        if (!artifact.IsSuccess) return artifact.Error;
        var output = await exporter.ExportAsync(artifact.Value, format, ct);
        if (!output.IsSuccess) return output.Error;
        var recheck = await reader.HandleAsync(actor, id, artifact.Value.Version, ct);
        if (!recheck.IsSuccess) return recheck.Error;
        return new ExportedArtifact(artifact.Value.Resource.Name + "." + format, output.Value);
    }
}
