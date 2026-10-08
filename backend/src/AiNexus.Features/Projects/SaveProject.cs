using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>An edit names the <c>ExpectedVersion</c> it was based on; a newer saved version is a conflict.</summary>
public sealed record ProjectRequest(string Name, string Description = "", string Instructions = "", int ExpectedVersion = 1, bool IsArchived = false);

/// <summary>
/// Description, instructions and version. An invalid name is reported first, with its own code, by the handler, so
/// these rules only apply once the name is valid.
/// </summary>
internal sealed class ProjectRequestValidator : RequestValidator<ProjectRequest>
{
    public override string ProblemCode => "project_settings_invalid";

    public ProjectRequestValidator()
    {
        When(x => ProjectQueries.NameIsValid(x.Name), () =>
        {
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Instructions).MaximumLength(4000);
            RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(1);
        });
    }
}

/// <summary>Creates a personal project (at most 100), or saves a new version of one the user may edit.</summary>
internal sealed class SaveProject(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("", async (ProjectRequest body, ICurrentUser user, SaveProject handler, CancellationToken ct) => (await handler.CreateAsync(user.Id, body, ct)).ToHttpResult())
        .Produces<ProjectDto>();

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", async (Guid id, ProjectRequest body, ICurrentUser user, SaveProject handler, CancellationToken ct) => (await handler.UpdateAsync(user.Id, id, body, ct)).ToHttpResult())
        .Produces<ProjectDto>();

    public async Task<Result<ProjectDto>> CreateAsync(Guid actor, ProjectRequest request, CancellationToken ct)
    {
        if (!ProjectQueries.NameIsValid(request.Name)) return ProjectErrors.InvalidName;
        if (await db.Set<WorkspaceResource>().CountAsync(x => x.OwnerId == actor && x.Kind == Project.Kind, ct) >= Project.MaxPerOwner) return ProjectErrors.Limit;
        var resource = new WorkspaceResource { OwnerId = actor, Name = ResourceAccess.Name(request.Name), Kind = Project.Kind };
        db.Add(resource); db.Add(new Project { Id = resource.Id, Description = request.Description.Trim(), Instructions = request.Instructions.Trim() });
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource.Id, Action = "project.created", Result = "created" });
        await db.SaveChangesAsync(ct);
        return await access.LoadProjectAsync(db, actor, resource.Id, ct);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid actor, Guid id, ProjectRequest request, CancellationToken ct)
    {
        if (!ProjectQueries.NameIsValid(request.Name)) return ProjectErrors.InvalidName;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var resource = await access.RequireAsync(actor, id, Project.Kind, ct, write: true);
            var count = await db.Set<Project>().Where(x => x.Id == id && x.Version == request.ExpectedVersion).ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.Description, request.Description.Trim()).SetProperty(x => x.Instructions, request.Instructions.Trim()).SetProperty(x => x.IsArchived, request.IsArchived), ct);
            if (count != 1) return ProjectErrors.Conflict;
            resource.Name = ResourceAccess.Name(request.Name); resource.UpdatedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "project.updated", Result = "saved" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await access.LoadProjectAsync(db, actor, id, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
