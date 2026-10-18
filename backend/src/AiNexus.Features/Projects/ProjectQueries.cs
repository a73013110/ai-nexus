using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

internal static class ProjectQueries
{
    /// <summary>操作者能看到的專案內容。</summary>
    public static async Task<Result<ProjectDto>> LoadProjectAsync(this ResourceAccess access, AppDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, Project.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var project = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return project.ToDto(await access.DescribeAsync(actor, resource.Value, ct));
    }

    /// <summary>先檢查讀取（或編輯）權限，再檢查專案未被封存。</summary>
    public static async Task<Result<Project>> RequireActiveAsync(this ResourceAccess access, AppDbContext db, Guid actor, Guid id, bool write, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct, write) is { IsSuccess: false } denied) return denied.Error;
        var row = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (row.IsArchived) return ProjectsErrors.Archived;
        return row;
    }
}
