using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Projects;

/// <summary>Shared instructions and reference files for conversations. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same id.</summary>
public sealed class Project
{
    public const string Kind = "project";
    public const int MaxPerOwner = 100;
    public const int MaxFiles = 50;

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public string Instructions { get; set; } = "";
    public int Version { get; set; } = 1;
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
