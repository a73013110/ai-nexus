using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects;

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

    /// <summary>專案的參考文件資源；新增時的 <see cref="Project.MaxFiles"/> 上限、列表與提示詞上下文都以同一個條件計算。</summary>
    public static IQueryable<Resource> ProjectFiles(this AppDbContext db, Guid id) => db.Set<Resource>().Where(x => x.ParentId == id && x.Kind == Document.Kind);
}
