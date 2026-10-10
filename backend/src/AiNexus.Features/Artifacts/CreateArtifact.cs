using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

public sealed record CreateArtifactRequest(string Title, string Content, Guid? SourceMessageId = null, Guid? ProjectId = null);

/// <summary>Content only. An invalid title is reported first, with its own code, by the handler.</summary>
internal sealed class CreateArtifactRequestValidator : RequestValidator<CreateArtifactRequest>
{
    public override string ProblemCode => ArtifactsErrors.ContentInvalidCode;

    public CreateArtifactRequestValidator()
    {
        When(x => Artifact.TitleIsValid(x.Title), () => RuleFor(x => x.Content).Must(Artifact.ContentIsValid).WithErrorCode("invalid"));
    }
}

/// <summary>
/// Creates a personal artifact (at most 200), optionally from a message the user owns and inside a project they may edit.
/// Also reached through <see cref="ArtifactService"/> by callers that bypass the request filter, so it checks the body itself.
/// </summary>
internal sealed class CreateArtifact(NexusDbContext db, ResourceAccess access, ConversationService conversations, AccessService features, GetArtifact reader, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", async (CreateArtifactRequest request, ICurrentUser user, CreateArtifact handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, request, ct)).ToHttpResult())
        .WithName("CreateArtifact");

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, CreateArtifactRequest request, CancellationToken ct)
    {
        if (!Artifact.TitleIsValid(request.Title)) return ArtifactsErrors.InvalidName;
        if (!Artifact.ContentIsValid(request.Content)) return ArtifactsErrors.ContentInvalid;
        if (request.SourceMessageId is Guid message)
        {
            var conversation = await db.Messages.Where(x => x.Id == message).Select(x => (Guid?)x.ConversationId).SingleOrDefaultAsync(ct);
            if (conversation is null) return ArtifactsErrors.MessageNotFound;
            var owned = await conversations.OwnedAsync(actor, conversation.Value, ct);
            if (!owned.IsSuccess) return owned.Error;
        }
        if (await db.Set<WorkspaceResource>().CountAsync(x => x.OwnerId == actor && x.Kind == Artifact.Kind, ct) >= Artifact.MaxPerOwner) return ArtifactsErrors.LimitReached;
        if (request.ProjectId is Guid project)
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == FeatureIds.Projects)) return ArtifactsErrors.ProjectAccessRequired;
            var editable = await access.RequireAsync(actor, project, "project", ct, write: true);
            if (!editable.IsSuccess) return editable.Error;
        }
        var now = clock.GetUtcNow();
        var resource = new WorkspaceResource { OwnerId = actor, ParentId = request.ProjectId, Kind = Artifact.Kind, Name = request.Title.Trim(), CreatedAt = now, UpdatedAt = now };
        db.Add(resource);
        db.Add(new Artifact { Id = resource.Id, SourceMessageId = request.SourceMessageId, ProjectId = request.ProjectId });
        db.Add(new ArtifactRevision { ArtifactId = resource.Id, Version = 1, AuthorId = actor, Title = resource.Name, Content = request.Content, CreatedAt = now });
        await db.SaveChangesAsync(ct);
        return await reader.HandleAsync(actor, resource.Id, null, ct);
    }
}
