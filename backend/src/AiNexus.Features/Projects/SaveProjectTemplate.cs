using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

public sealed record ProjectTemplateRequest(string Title, string Content);

internal sealed class ProjectTemplateRequestValidator : RequestValidator<ProjectTemplateRequest>
{
    public override string ProblemCode => "project_template_invalid";

    public ProjectTemplateRequestValidator()
    {
        RuleFor(x => x.Title).Must(x => x?.Trim().Length is >= 1 and <= ProjectTemplate.TitleMaxLength).WithErrorCode("length");
        RuleFor(x => x.Content).Must(x => x?.Trim().Length is >= 1 and <= ProjectTemplate.ContentMaxLength).WithErrorCode("length");
    }
}

/// <summary>Adds a template (at most 30) to, or edits one of, an active project the user may edit.</summary>
internal sealed class SaveProjectTemplate(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/templates", async (Guid id, ProjectTemplateRequest body, ICurrentUser user, SaveProjectTemplate handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, null, body, ct)).ToHttpResult())
        .Produces<ProjectTemplateDto>();

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/templates/{key:guid}", async (Guid id, Guid key, ProjectTemplateRequest body, ICurrentUser user, SaveProjectTemplate handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, key, body, ct)).ToHttpResult())
        .Produces<ProjectTemplateDto>();

    public async Task<Result<ProjectTemplateDto>> HandleAsync(Guid actor, Guid id, Guid? key, ProjectTemplateRequest request, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        ProjectTemplate? row;
        if (key is Guid existing)
        {
            row = await db.Set<ProjectTemplate>().SingleOrDefaultAsync(x => x.Id == existing && x.ProjectId == id, ct);
            if (row is null) return ProjectErrors.TemplateMissing;
        }
        else
        {
            if (await db.Set<ProjectTemplate>().CountAsync(x => x.ProjectId == id, ct) >= ProjectTemplate.MaxPerProject) return ProjectErrors.TemplateLimit;
            row = new() { ProjectId = id }; db.Add(row);
        }
        row.Title = request.Title.Trim(); row.Content = request.Content.Trim();
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = row.Id, Action = key is null ? "project.template.created" : "project.template.updated", Result = "saved" });
        await db.SaveChangesAsync(ct);
        return row.ToDto();
    }
}
