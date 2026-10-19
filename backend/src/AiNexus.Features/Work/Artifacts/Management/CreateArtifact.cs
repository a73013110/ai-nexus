using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Data.App.Collaboration;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Artifacts.Management;

public sealed record CreateArtifactRequest(string Title, string Content, Guid? SourceMessageId = null, Guid? ProjectId = null);

/// <summary>只驗證內容；標題不合法時由 handler 先回傳自己的錯誤代碼。</summary>
internal sealed class CreateArtifactRequestValidator : RequestValidator<CreateArtifactRequest>
{
    public override string ProblemCode => ArtifactsErrors.ContentInvalidCode;

    public CreateArtifactRequestValidator()
    {
        When(x => Artifact.TitleIsValid(x.Title), () => RuleFor(x => x.Content).Must(Artifact.ContentIsValid).WithErrorCode("invalid"));
    }
}

/// <summary>
/// 建立個人成果（每人上限 200 份），可選擇由使用者擁有的訊息產生，並可放入使用者有編輯權的專案。
/// 其他模組會經由 <see cref="ArtifactService"/> 直接呼叫、不經請求過濾器，因此這裡自己檢查內文。
/// </summary>
internal sealed class CreateArtifact(
    AppDbContext db,
    ResourceAccess access,
    ConversationService conversations,
    AccessService features,
    GetArtifact reader,
    TimeProvider clock)
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
            var owned = await conversations.RequireOwnedAsync(actor, conversation.Value, ct);
            if (!owned.IsSuccess) return owned.Error;
        }
        var count = await db.Set<Resource>().CountAsync(x => x.OwnerId == actor && x.Kind == Artifact.Kind, ct);
        if (count >= Artifact.MaxPerOwner) return ArtifactsErrors.LimitReached;
        if (request.ProjectId is Guid project)
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == FeatureCatalog.Projects)) return AccessControlErrors.FeatureForbidden;
            var editable = await access.RequireAsync(actor, project, "project", ct, write: true);
            if (!editable.IsSuccess) return editable.Error;
        }
        var now = clock.GetUtcNow();
        var resource = new Resource
        {
            OwnerId = actor,
            ParentId = request.ProjectId,
            Kind = Artifact.Kind,
            Name = request.Title.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(resource);
        db.Add(new Artifact { Id = resource.Id, SourceMessageId = request.SourceMessageId, ProjectId = request.ProjectId });
        db.Add(new ArtifactRevision
        {
            ArtifactId = resource.Id,
            Version = 1,
            AuthorId = actor,
            Title = resource.Name,
            Content = request.Content,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return await reader.HandleAsync(actor, resource.Id, null, ct);
    }
}
