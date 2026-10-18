using AiNexus.Data.App.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Projects;

/// <summary>供對話共用的指示與參考檔案。名稱、擁有者與分享設定存放在同一 id 的 <see cref="Resource"/> 上。</summary>
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
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> row)
    {
        row.ToTable("Projects", "projects");
        row.HasKey(x => x.Id);
        row.Property(x => x.Description).HasMaxLength(2000);
        row.Property(x => x.Instructions).HasMaxLength(4000);
        row.HasOne<Resource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
    }
}
