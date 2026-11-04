using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Templates;

public sealed record ProjectTemplateRequest(string Title, string Content);

internal sealed class ProjectTemplateRequestValidator : RequestValidator<ProjectTemplateRequest>
{
    public override Error Problem => ProjectsErrors.TemplateInvalid;

    public ProjectTemplateRequestValidator()
    {
        RuleFor(x => x.Title).RequiredText(ProjectTemplate.TitleMaxLength);
        RuleFor(x => x.Content).RequiredContent(ProjectTemplate.ContentMaxLength);
    }
}

/// <summary>在可編輯的有效專案中新增範本（每個專案最多 30 個）或編輯既有範本。</summary>
internal sealed class SaveProjectTemplate(AppDbContext db, ResourceAccess access, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapPost("/{id:guid}/templates", (Guid id, ProjectTemplateRequest body, ICurrentUser user, SaveProjectTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, null, body, ct)).Audited(ProjectsAudit.TemplateCreated);

        routes.MapPut("/{id:guid}/templates/{key:guid}", (Guid id, Guid key, ProjectTemplateRequest body, ICurrentUser user, SaveProjectTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, key, body, ct)).Audited(ProjectsAudit.TemplateUpdated);
    }

    public async Task<Result<ProjectTemplateDto>> HandleAsync(Guid actor, Guid id, Guid? key, ProjectTemplateRequest request, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        ProjectTemplate? row;
        if (key is Guid existing)
        {
            row = await db.ProjectTemplates.SingleOrDefaultAsync(x => x.Id == existing && x.ProjectId == id, ct);
            if (row is null) return ProjectsErrors.TemplateMissing;
        }
        else
        {
            if (await db.ProjectTemplates.CountAsync(x => x.ProjectId == id, ct) >= ProjectTemplate.MaxPerProject) return ProjectsErrors.TemplateLimit;
            row = new() { ProjectId = id };
            db.Add(row);
        }
        row.Title = request.Title.Trim(); row.Content = request.Content.Trim();
        audit.Describe(resource: row.Id);
        await db.SaveChangesAsync(ct);
        return row.ToDto();
    }
}
