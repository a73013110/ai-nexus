using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

[Comment("功能群組與功能的授權關聯；有效功能取聯集。")]
public sealed class RoleGroupFeature
{
    [Comment("關聯功能群組的識別碼。")]
    public string GroupId { get; set; } = "";
    [Comment("關聯功能的識別碼。")]
    public string FeatureId { get; set; } = "";
}

internal sealed class RoleGroupFeatureConfiguration : IEntityTypeConfiguration<RoleGroupFeature>
{
    public void Configure(EntityTypeBuilder<RoleGroupFeature> grant)
    {
        grant.ToTable("RoleGroupFeatures", "accesscontrol");
        grant.HasKey(x => new { x.GroupId, x.FeatureId });
        grant.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        grant.HasOne<Feature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
    }
}
