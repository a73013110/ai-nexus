using System.Text.Json;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

/// <summary>An edit names the <c>ExpectedVersion</c> it was based on; a newer saved version is a conflict.</summary>
public sealed record SaveArtifactRequest(string Title, string Content, int ExpectedVersion);

/// <summary>Content only. An invalid title is reported first, with its own code, by the handler.</summary>
internal sealed class SaveArtifactRequestValidator : RequestValidator<SaveArtifactRequest>
{
    public override string ProblemCode => ArtifactErrors.ContentInvalidCode;

    public SaveArtifactRequestValidator()
    {
        When(x => Artifact.TitleIsValid(x.Title), () => RuleFor(x => x.Content).Must(Artifact.ContentIsValid).WithErrorCode("invalid"));
    }
}

/// <summary>Adds an immutable revision to an artifact the user may edit. Never overwrites a version saved in the meantime.</summary>
internal sealed class SaveArtifact(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, GetArtifact reader, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", async (Guid id, SaveArtifactRequest request, ICurrentUser user, SaveArtifact handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, request, ct)).ToHttpResult())
        .WithName("SaveArtifact").Produces<ArtifactDto>();

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, Guid id, SaveArtifactRequest request, CancellationToken ct)
    {
        if (!Artifact.TitleIsValid(request.Title)) return ArtifactErrors.InvalidName;
        if (request.ExpectedVersion is < 1 or >= Artifact.MaxVersions) return ArtifactErrors.VersionLimit;
        using (await writes.AcquireAsync(id, ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var resource = await access.RequireAsync(actor, id, Artifact.Kind, ct, write: true);
            var changed = await db.Set<Artifact>().Where(x => x.Id == id && x.Version == request.ExpectedVersion).ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1), ct);
            if (changed != 1) return ArtifactErrors.VersionConflict;
            var now = clock.GetUtcNow();
            resource.Name = request.Title.Trim(); resource.UpdatedAt = now;
            db.Add(new ArtifactRevision { ArtifactId = id, Version = request.ExpectedVersion + 1, AuthorId = actor, Title = resource.Name, Content = request.Content, CreatedAt = now });
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "artifact.revised", Result = "saved", DetailsJson = JsonSerializer.Serialize(new { version = request.ExpectedVersion + 1 }) });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return await reader.HandleAsync(actor, id, null, ct);
        }
    }
}
