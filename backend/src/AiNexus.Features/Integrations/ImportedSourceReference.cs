using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Integrations;

/// <summary>Provenance of a private snapshot imported from a read-only source.</summary>
public sealed class ImportedSourceReference
{
    public Guid ArtifactId { get; set; }
    public string SourceId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class ImportedSourceReferenceConfiguration : IEntityTypeConfiguration<ImportedSourceReference>
{
    public void Configure(EntityTypeBuilder<ImportedSourceReference> r)
    {
        r.ToTable("SourceReferences", "content"); r.HasKey(x => x.ArtifactId);
        r.Property(x => x.SourceId).HasMaxLength(32); r.Property(x => x.ExternalId).HasMaxLength(160); r.Property(x => x.Revision).HasMaxLength(160);
        r.HasIndex(x => new { x.SourceId, x.ExternalId });
    }
}
