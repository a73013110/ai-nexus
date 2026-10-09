using AiNexus.Features.Collaboration;
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
