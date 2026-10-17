using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects.Management;

/// <summary>編輯時需帶上所依據的 <c>ExpectedVersion</c>；若已有更新的已儲存版本，就視為衝突。</summary>
public sealed record ProjectRequest(string Name, string Description = "", string Instructions = "", int ExpectedVersion = 1, bool IsArchived = false);

/// <summary>
/// 描述、指示與版本。名稱不合法時，handler 會先以自己的錯誤代碼回報，因此這些規則只在名稱合法後才適用。
/// </summary>
internal sealed class ProjectRequestValidator : RequestValidator<ProjectRequest>
{
    public override string ProblemCode => "project_settings_invalid";

    public ProjectRequestValidator()
    {
        When(x => ResourceAccess.IsValidName(x.Name), () =>
        {
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Instructions).MaximumLength(4000);
            RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(1);
        });
    }
}

/// <summary>建立個人專案（最多 100 個），或儲存使用者可編輯的專案的新版本。</summary>
internal sealed class SaveProject(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("", async (ProjectRequest body, ICurrentUser user, SaveProject handler, CancellationToken ct) => (await handler.CreateAsync(user.Id, body, ct)).ToHttpResult());

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", async (Guid id, ProjectRequest body, ICurrentUser user, SaveProject handler, CancellationToken ct) => (await handler.UpdateAsync(user.Id, id, body, ct)).ToHttpResult());

    public async Task<Result<ProjectDto>> CreateAsync(Guid actor, ProjectRequest request, CancellationToken ct)
    {
        if (ResourceAccess.Name(request.Name) is not { IsSuccess: true } name) return ProjectsErrors.InvalidName;
        if (await db.Set<WorkspaceResource>().CountAsync(x => x.OwnerId == actor && x.Kind == Project.Kind, ct) >= Project.MaxPerOwner) return ProjectsErrors.Limit;
        var resource = new WorkspaceResource { OwnerId = actor, Name = name.Value, Kind = Project.Kind };
        db.Add(resource);
        db.Add(new Project { Id = resource.Id, Description = request.Description.Trim(), Instructions = request.Instructions.Trim() });
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource.Id, Action = "project.created", Result = "created" });
        await db.SaveChangesAsync(ct);
        return await access.LoadProjectAsync(db, actor, resource.Id, ct);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid actor, Guid id, ProjectRequest request, CancellationToken ct)
    {
        if (ResourceAccess.Name(request.Name) is not { IsSuccess: true } name) return ProjectsErrors.InvalidName;
        using (await writes.AcquireAsync(id, ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var allowed = await access.RequireAsync(actor, id, Project.Kind, ct, write: true);
            if (!allowed.IsSuccess) return allowed.Error;
            var resource = allowed.Value;
            var count = await db.Set<Project>().Where(x => x.Id == id && x.Version == request.ExpectedVersion).ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.Description, request.Description.Trim()).SetProperty(x => x.Instructions, request.Instructions.Trim()).SetProperty(x => x.IsArchived, request.IsArchived), ct);
            if (count != 1) return ProjectsErrors.Conflict;
            resource.Name = name.Value;
            resource.UpdatedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "project.updated", Result = "saved" });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await access.LoadProjectAsync(db, actor, id, ct);
        }
    }
}
