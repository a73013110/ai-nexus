using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Projects;

/// <summary>A reusable opening prompt of a project.</summary>
public sealed class ProjectTemplate
{
    public const int TitleMaxLength = 80;
    public const int ContentMaxLength = 12000;
    public const int MaxPerProject = 30;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";

    public ProjectTemplateDto ToDto() => new(Id, Title, Content);
}

public sealed record ProjectTemplateDto(Guid Id, string Title, string Content);

internal sealed class ProjectTemplateConfiguration : IEntityTypeConfiguration<ProjectTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTemplate> template)
    {
        template.ToTable("ProjectTemplates", "projects"); template.HasKey(x => x.Id);
        template.Property(x => x.Title).HasMaxLength(ProjectTemplate.TitleMaxLength); template.Property(x => x.Content).HasMaxLength(ProjectTemplate.ContentMaxLength);
        template.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
