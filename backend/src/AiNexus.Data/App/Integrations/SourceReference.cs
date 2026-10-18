using AiNexus.Data.App.Artifacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Integrations;

/// <summary>從唯讀來源匯入的私人快照的出處資訊。</summary>
public sealed class SourceReference
{
    public Guid ArtifactId { get; set; }
    public string SourceId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
}

internal sealed class SourceReferenceConfiguration : IEntityTypeConfiguration<SourceReference>
{
    public void Configure(EntityTypeBuilder<SourceReference> r)
    {
        r.Property(x => x.ImportedAt).HasValueGenerator<CreationTime>();
        r.ToTable("SourceReferences", "integrations");
        r.HasKey(x => x.ArtifactId);
        r.Property(x => x.SourceId).HasMaxLength(32);
        r.Property(x => x.ExternalId).HasMaxLength(160);
        r.Property(x => x.Revision).HasMaxLength(160);
        r.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
    }
}
