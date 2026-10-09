using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

public sealed class RoleGroupFeature
{
    public string GroupId { get; set; } = "";
    public string FeatureId { get; set; } = "";
}

internal sealed class RoleGroupFeatureConfiguration : IEntityTypeConfiguration<RoleGroupFeature>
{
    public void Configure(EntityTypeBuilder<RoleGroupFeature> grant)
    {
        grant.ToTable("RoleGroupFeatures", "access");
        grant.HasKey(x => new { x.GroupId, x.FeatureId });
        grant.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        grant.HasOne<Feature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
    }
}
