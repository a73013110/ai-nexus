using AiNexus.Data.App;
using AiNexus.Data.App.Integrations;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Identity;
using AiNexus.Features.Integrations.Sources;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Integrations.Handoff;

internal sealed class SourceImportRequestValidator : RequestValidator<SourceImportRequest>
{
    public override string ProblemCode => "source_id_invalid";

    public SourceImportRequestValidator()
    {
        RuleFor(x => x.RecordId).Must(SourceGateway.ValidRecordId).WithErrorCode("invalid");
        RuleFor(x => x.ExpectedRevision).MaximumLength(160);
    }
}

/// <summary>把紀錄複製成一份私人成果。快照不授予任何人存取即時來源的權限。</summary>
internal sealed class ImportSourceRecord(
    AppDbContext db,
    AccessService access,
    SourceGateway gateway,
    ArtifactService artifacts,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{source}/import", async (
            string source, SourceImportRequest body, ICurrentUser user, ImportSourceRecord handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, source, body, ct)).ToHttpResult());

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, string source, SourceImportRequest request, CancellationToken ct)
    {
        if (!(await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == FeatureIds.Artifacts))
            return AccessControlErrors.FeatureForbidden;
        var read = await gateway.ReadAsync(actor, source, request.RecordId, ct);
        if (!read.IsSuccess) return read.Error;
        var detail = read.Value;
        if (detail.Record.Revision != request.ExpectedRevision) return IntegrationsErrors.Changed;
        var provenance = JsonSerializer.Serialize(
            new
            {
                source,
                id = detail.Record.Id,
                version = detail.Record.Revision,
                modifiedAt = detail.Record.ModifiedAt,
                importedAt = clock.GetUtcNow(),
            },
            Indented);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var body = detail.Body + "\n\n---\n\n### 匯入來源（當時快照）\n\n```json\n" + provenance + "\n```";
        var created = await artifacts.CreateAsync(actor, new(detail.Record.Title, body), ct);
        if (!created.IsSuccess) return created.Error;
        var artifact = created.Value;
        db.Add(new SourceReference
        {
            ArtifactId = artifact.Resource.Id,
            SourceId = source,
            ExternalId = detail.Record.Id,
            Revision = detail.Record.Revision,
            ImportedAt = clock.GetUtcNow(),
        });
        db.AuditEvents.Add(new()
        {
            OwnerId = actor,
            ResourceId = artifact.Resource.Id,
            Action = "integration.snapshot.imported",
            Result = "private",
            DetailsJson = JsonSerializer.Serialize(new { source, externalId = detail.Record.Id }),
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return artifact;
    }
}
