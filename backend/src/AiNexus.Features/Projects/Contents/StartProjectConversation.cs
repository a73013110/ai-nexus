using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects.Templates;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects.Contents;

/// <summary>範本的標題會取代 <c>Title</c>，範本內容則以預填提示詞的形式回傳。</summary>
[ValidatedInHandler("The title is checked after project access, and a template's title replaces it.")]
public sealed record ProjectConversationRequest(string? Title = null, Guid? TemplateId = null);
public sealed record ProjectConversationDto(ConversationDto Conversation, string Prompt);

/// <summary>在可讀取的有效專案中開啟使用者自己的對話；需要聊天功能。</summary>
internal sealed class StartProjectConversation(NexusDbContext db, ResourceAccess access, AccessService features)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/conversations", async (Guid id, ProjectConversationRequest body, ICurrentUser user, StartProjectConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult());

    public async Task<Result<ProjectConversationDto>> HandleAsync(Guid actor, Guid id, ProjectConversationRequest request, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: false, ct);
        if (!active.IsSuccess) return active.Error;
        if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "chat")) return AccessControlErrors.FeatureForbidden;
        var prompt = ""; var title = request.Title ?? "新對話";
        if (request.TemplateId is Guid key)
        {
            var template = await db.Set<ProjectTemplate>().SingleOrDefaultAsync(x => x.Id == key && x.ProjectId == id, ct);
            if (template is null) return ProjectsErrors.TemplateMissing;
            title = template.Title; prompt = template.Content;
        }
        if (ResourceAccess.Name(title) is not { IsSuccess: true } name) return ProjectsErrors.InvalidName;
        var row = new Conversation { OwnerId = actor, ProjectId = id, Title = name.Value };
        db.Add(row); await db.SaveChangesAsync(ct);
        return new ProjectConversationDto(row.ToDto(), prompt);
    }
}
