using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Projects;

/// <summary>Shared instructions and reference files for conversations. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same id.</summary>
[Comment("專案資源、共用指令及範本版本。")]
public sealed class Project
{
    public const string Kind = "project";
    public const int MaxPerOwner = 100;
    public const int MaxFiles = 50;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("業務物件的用途說明。")]
    public string Description { get; set; } = "";
    [Comment("專案共用指令。")]
    public string Instructions { get; set; } = "";
    [Comment("業務版本號，用於歷史或樂觀並行控制。")]
    public int Version { get; set; } = 1;
    [Comment("是否封存對話；封存後不再接受新的生成。")]
    public bool IsArchived { get; set; }

    public ProjectDto ToDto(ResourceDto resource) => new(resource, Description, Instructions, Version, IsArchived);
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> row)
    {
        row.ToTable("Projects", "projects"); row.HasKey(x => x.Id);
        row.Property(x => x.Description).HasMaxLength(2000); row.Property(x => x.Instructions).HasMaxLength(4000);
    }
}

internal static class ProjectQueries
{
    /// <summary>The project as the actor may see it.</summary>
    public static async Task<Result<ProjectDto>> LoadProjectAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, Project.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var project = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return project.ToDto(await access.DescribeAsync(actor, resource.Value, ct));
    }

    /// <summary>Read (or edit) access first, then the project must not be archived.</summary>
    public static async Task<Result<Project>> RequireActiveAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, bool write, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct, write) is { IsSuccess: false } denied) return denied.Error;
        var row = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (row.IsArchived) return ProjectsErrors.Archived;
        return row;
    }
}
