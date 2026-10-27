using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Work.Projects.Templates;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Contents;

/// <summary>範本的標題會取代 <c>Title</c>，範本內容則以預填提示詞的形式回傳。</summary>
[ValidatedInHandler("The title is checked after project access, and a template's title replaces it.")]
public sealed record ProjectConversationRequest(string? Title = null, Guid? TemplateId = null);
public sealed record ProjectConversationDto(ConversationDto Conversation, string Prompt);

/// <summary>在可讀取的有效專案中開啟使用者自己的對話；需要聊天功能。</summary>
internal sealed class StartProjectConversation(AppDbContext db, ResourceAccess access, AccessService features)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/conversations", async (Guid id, ProjectConversationRequest body, ICurrentUser user, StartProjectConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult()).Audited(ProjectsAudit.ConversationCreated);

    public async Task<Result<ProjectConversationDto>> HandleAsync(Guid actor, Guid id, ProjectConversationRequest request, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: false, ct);
        if (!active.IsSuccess) return active.Error;
        if (!await features.HoldsAsync(actor, FeatureCatalog.Chat, ct)) return AccessControlErrors.FeatureForbidden;
        var prompt = "";
        var title = request.Title ?? CreateConversation.DefaultTitle;
        if (request.TemplateId is Guid key)
        {
            var template = await db.Set<ProjectTemplate>().SingleOrDefaultAsync(x => x.Id == key && x.ProjectId == id, ct);
            if (template is null) return ProjectsErrors.TemplateMissing;
            title = template.Title;
            prompt = template.Content;
        }
        var name = ResourceAccess.Name(title);
        if (!name.IsSuccess) return name.Error;
        var row = new Conversation { OwnerId = actor, ProjectId = id, Title = name.Value };
        db.Add(row);
        await db.SaveChangesAsync(ct);
        return new ProjectConversationDto(row.ToDto(), prompt);
    }
}
