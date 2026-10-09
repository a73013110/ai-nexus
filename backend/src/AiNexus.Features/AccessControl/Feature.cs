using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>A navigation entry and the grant behind it. Rows are seeded by each owning module's <see cref="FeatureSeed"/>.</summary>
public sealed class Feature
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Route { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
}

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> feature)
    {
        feature.ToTable("Features", "access");
        feature.HasKey(x => x.Id);
        feature.Property(x => x.Id).HasMaxLength(64);
        feature.Property(x => x.Name).HasMaxLength(120);
        feature.Property(x => x.Route).HasMaxLength(160);
    }
}
