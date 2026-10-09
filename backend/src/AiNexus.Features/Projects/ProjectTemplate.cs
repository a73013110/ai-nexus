using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Projects;

/// <summary>A reusable opening prompt of a project.</summary>
[Comment("專案建立範本與預設指令。")]
public sealed class ProjectTemplate
{
    public const int TitleMaxLength = 80;
    public const int ContentMaxLength = 12000;
    public const int MaxPerProject = 30;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("關聯專案的識別碼。")]
    public Guid ProjectId { get; set; }
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "";
    [Comment("範本的開場提示內容。")]
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
