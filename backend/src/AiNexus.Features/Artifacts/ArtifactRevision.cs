using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Artifacts;

[Comment("成果文件不可變版本、內容與作者。")]
public sealed class ArtifactRevision
{
    [Comment("關聯成果文件的識別碼。")]
    public Guid ArtifactId { get; set; }
    [Comment("業務版本號，用於歷史或樂觀並行控制。")]
    public int Version { get; set; }
    [Comment("建立此版本的使用者識別碼。")]
    public Guid AuthorId { get; set; }
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "";
    [Comment("此版本的成果內容。")]
    public string Content { get; set; } = "";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class ArtifactRevisionConfiguration : IEntityTypeConfiguration<ArtifactRevision>
{
    public void Configure(EntityTypeBuilder<ArtifactRevision> revision)
    {
        revision.ToTable("ArtifactRevisions", "artifacts"); revision.HasKey(x => new { x.ArtifactId, x.Version });
        revision.Property(x => x.Title).HasMaxLength(Artifact.TitleMaxLength); revision.Property(x => x.Content).HasMaxLength(Artifact.ContentMaxLength);
        revision.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
    }
}
