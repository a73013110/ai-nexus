using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Artifacts;

public sealed class ArtifactRevision
{
    public Guid ArtifactId { get; set; }
    public int Version { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class ArtifactRevisionConfiguration : IEntityTypeConfiguration<ArtifactRevision>
{
    public void Configure(EntityTypeBuilder<ArtifactRevision> revision)
    {
        revision.ToTable("ArtifactRevisions", "content"); revision.HasKey(x => new { x.ArtifactId, x.Version });
        revision.Property(x => x.Title).HasMaxLength(Artifact.TitleMaxLength); revision.Property(x => x.Content).HasMaxLength(Artifact.ContentMaxLength);
        revision.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
    }
}
