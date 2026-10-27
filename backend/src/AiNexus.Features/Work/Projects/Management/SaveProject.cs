using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Management;

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
internal sealed class SaveProject(AppDbContext db, ResourceAccess access, ResourceWriteLock writes, TimeProvider clock, AuditTrail audit)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("", async (ProjectRequest body, ICurrentUser user, SaveProject handler, CancellationToken ct) => (await handler.CreateAsync(user.Id, body, ct)).ToHttpResult()).Audited(ProjectsAudit.ProjectCreated);

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", async (Guid id, ProjectRequest body, ICurrentUser user, SaveProject handler, CancellationToken ct) => (await handler.UpdateAsync(user.Id, id, body, ct)).ToHttpResult()).Audited(ProjectsAudit.ProjectUpdated);

    public async Task<Result<ProjectDto>> CreateAsync(Guid actor, ProjectRequest request, CancellationToken ct)
    {
        var name = ResourceAccess.Name(request.Name);
        if (!name.IsSuccess) return name.Error;
        if (await db.Set<Resource>().CountAsync(x => x.OwnerId == actor && x.Kind == Project.Kind, ct) >= Project.MaxPerOwner) return ProjectsErrors.Limit;
        var resource = new Resource { OwnerId = actor, Name = name.Value, Kind = Project.Kind };
        db.Add(resource);
        db.Add(new Project { Id = resource.Id, Description = request.Description.Trim(), Instructions = request.Instructions.Trim() });
        audit.Describe(resource: resource.Id);
        await db.SaveChangesAsync(ct);
        return await access.LoadProjectAsync(db, actor, resource.Id, ct);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid actor, Guid id, ProjectRequest request, CancellationToken ct)
    {
        var name = ResourceAccess.Name(request.Name);
        if (!name.IsSuccess) return name.Error;
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
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await access.LoadProjectAsync(db, actor, id, ct);
        }
    }
}
