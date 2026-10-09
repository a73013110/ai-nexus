using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class EmbeddingProfile
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int Dimensions { get; set; }
    public string InputFormat { get; set; } = "plain";
    public string QueryInstruction { get; set; } = "";
    public string Revision { get; set; } = "";
    public string ChunkerConfiguration { get; set; } = "";
    public string Status { get; set; } = "building";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}

internal sealed class EmbeddingProfileConfiguration : IEntityTypeConfiguration<EmbeddingProfile>
{
    public void Configure(EntityTypeBuilder<EmbeddingProfile> profile)
    {
        profile.ToTable("EmbeddingProfiles", "knowledge"); profile.HasKey(x => x.Id);
        profile.Property(x => x.Key).HasMaxLength(200); profile.HasIndex(x => x.Key).IsUnique();
        profile.Property(x => x.Provider).HasMaxLength(32); profile.Property(x => x.Model).HasMaxLength(160); profile.Property(x => x.InputFormat).HasMaxLength(32);
        profile.Property(x => x.QueryInstruction).HasMaxLength(500); profile.Property(x => x.Revision).HasMaxLength(64); profile.Property(x => x.ChunkerConfiguration).HasMaxLength(500);
        profile.Property(x => x.Status).HasMaxLength(16); profile.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] = 'active'");
    }
}
