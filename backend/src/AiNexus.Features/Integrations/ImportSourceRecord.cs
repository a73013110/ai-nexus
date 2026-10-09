using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Integrations;

internal sealed class SourceImportRequestValidator : RequestValidator<SourceImportRequest>
{
    public override string ProblemCode => "source_id_invalid";

    public SourceImportRequestValidator()
    {
        RuleFor(x => x.RecordId).Must(SourceGateway.ValidRecordId).WithErrorCode("invalid");
        RuleFor(x => x.ExpectedRevision).MaximumLength(160);
    }
}

/// <summary>Copies a record into a private artifact. The snapshot grants nobody access to the live source.</summary>
internal sealed class ImportSourceRecord(NexusDbContext db, AccessService access, SourceGateway gateway, ArtifactService artifacts, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{source}/import", async (string source, SourceImportRequest body, ICurrentUser user, ImportSourceRecord handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, source, body, ct)).ToHttpResult())
        .Produces<ArtifactDto>();

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, string source, SourceImportRequest request, CancellationToken ct)
    {
        if (!(await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == FeatureIds.Artifacts)) return IntegrationsErrors.ArtifactFeatureRequired;
        var read = await gateway.ReadAsync(actor, source, request.RecordId, ct);
        if (!read.IsSuccess) return read.Error;
        var detail = read.Value;
        if (detail.Record.Revision != request.ExpectedRevision) return IntegrationsErrors.Changed;
        var provenance = JsonSerializer.Serialize(new { source, id = detail.Record.Id, version = detail.Record.Revision, modifiedAt = detail.Record.ModifiedAt, importedAt = clock.GetUtcNow() }, Indented);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var artifact = await artifacts.CreateAsync(actor, new(detail.Record.Title, detail.Body + "\n\n---\n\n### 匯入來源（當時快照）\n\n```json\n" + provenance + "\n```"), ct);
        db.Add(new ImportedSourceReference { ArtifactId = artifact.Resource.Id, SourceId = source, ExternalId = detail.Record.Id, Revision = detail.Record.Revision, ImportedAt = clock.GetUtcNow() });
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = artifact.Resource.Id, Action = "integration.snapshot.imported", Result = "private", DetailsJson = JsonSerializer.Serialize(new { source, externalId = detail.Record.Id }) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return artifact;
    }
}
