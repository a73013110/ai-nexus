using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

public sealed class RoleGroupFeature
{
    public string GroupId { get; set; } = "";
    public string FeatureId { get; set; } = "";
    /// <summary>資料範圍（ADR 0017）；null 表示功能沒有資料列概念。可用值由功能目錄宣告。</summary>
    public string? Scope { get; set; }
}

internal sealed class RoleGroupFeatureConfiguration : IEntityTypeConfiguration<RoleGroupFeature>
{
    public void Configure(EntityTypeBuilder<RoleGroupFeature> grant)
    {
        grant.ToTable("RoleGroupFeatures", "accesscontrol");
        grant.HasKey(x => new { x.GroupId, x.FeatureId });
        grant.Property(x => x.Scope).HasMaxLength(16);
        grant.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        grant.HasOne<Feature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
    }
}
