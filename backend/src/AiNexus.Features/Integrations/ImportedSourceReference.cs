using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Integrations;

/// <summary>Provenance of a private snapshot imported from a read-only source.</summary>
[Comment("外部來源匯入的識別、來源版本與匯入時間。")]
public sealed class ImportedSourceReference
{
    [Comment("關聯成果文件的識別碼。")]
    public Guid ArtifactId { get; set; }
    [Comment("外部資料來源識別碼。")]
    public string SourceId { get; set; } = "";
    [Comment("外部來源中的紀錄識別碼。")]
    public string ExternalId { get; set; } = "";
    [Comment("外部來源或 repository 的固定版本識別。")]
    public string Revision { get; set; } = "";
    [Comment("此來源版本明確匯入的時間。")]
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class ImportedSourceReferenceConfiguration : IEntityTypeConfiguration<ImportedSourceReference>
{
    public void Configure(EntityTypeBuilder<ImportedSourceReference> r)
    {
        r.ToTable("SourceReferences", "integrations"); r.HasKey(x => x.ArtifactId);
        r.Property(x => x.SourceId).HasMaxLength(32); r.Property(x => x.ExternalId).HasMaxLength(160); r.Property(x => x.Revision).HasMaxLength(160);
        r.HasIndex(x => new { x.SourceId, x.ExternalId });
    }
}
