using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

internal static class ProjectQueries
{
    /// <summary>The rule of <see cref="ResourceAccess.Name"/>.</summary>
    public static bool NameIsValid(string? name) => name?.Trim() is { Length: >= 1 and <= 120 } trimmed && !trimmed.Any(char.IsControl);

    /// <summary>The project as the actor may see it. Access failures stay exceptions of <see cref="ResourceAccess"/>.</summary>
    public static async Task<ProjectDto> LoadProjectAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = (await access.RequireAsync(actor, id, Project.Kind, ct)).OrThrow();
        var project = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return project.ToDto(await access.DescribeAsync(actor, resource, ct));
    }

    /// <summary>Read (or edit) access first, then the project must not be archived. Access failures stay exceptions.</summary>
    public static async Task<Result<Project>> RequireActiveAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, bool write, CancellationToken ct)
    {
        (await access.RequireAsync(actor, id, Project.Kind, ct, write)).OrThrow();
        var row = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (row.IsArchived) return ProjectsErrors.Archived;
        return row;
    }
}
