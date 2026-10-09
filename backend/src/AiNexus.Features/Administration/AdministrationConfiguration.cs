using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Administration;

/// <summary>The administrator role, group and feature, seeded with <c>HasData</c>.</summary>
public sealed class AdministrationConfiguration() : FeatureSeed(new PlatformFeature(Feature, "平台管理", "/admin", 90, AdministratorsOnly: true)),
    IEntityTypeConfiguration<AccessControl.Role>, IEntityTypeConfiguration<RoleGroup>, IEntityTypeConfiguration<RoleGroupRole>
{
    public const string Role = "administrator", Group = BuiltInAccess.AdministratorsGroup, Feature = FeatureIds.Admin, Policy = Policies.Admin;

    public void Configure(EntityTypeBuilder<AccessControl.Role> builder) => builder.HasData(new AccessControl.Role { Id = Role, Name = "平台管理員" });
    public void Configure(EntityTypeBuilder<RoleGroup> builder) => builder.HasData(new RoleGroup { Id = Group, Name = "平台管理" });
    public void Configure(EntityTypeBuilder<RoleGroupRole> builder) => builder.HasData(new RoleGroupRole { RoleId = Role, GroupId = Group });
}
