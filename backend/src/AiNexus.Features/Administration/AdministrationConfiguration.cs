using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Administration;

/// <summary>系統管理員的角色、群組與功能種子資料，隨 migration 以 <c>HasData</c> 寫入。</summary>
public sealed class AdministrationConfiguration() : FeatureSeed(new PlatformFeature(Feature, "存取管理", "/admin", 90, BuiltInAccess.SystemNavigation, "lock", AdministratorsOnly: true)),
    IEntityTypeConfiguration<AccessControl.Roles.Role>, IEntityTypeConfiguration<RoleGroup>, IEntityTypeConfiguration<RoleGroupRole>
{
    public const string Role = "administrator", Group = BuiltInAccess.AdministratorsGroup, Feature = FeatureIds.Admin, Policy = Policies.Admin;

    public void Configure(EntityTypeBuilder<AccessControl.Roles.Role> builder) => builder.HasData(new AccessControl.Roles.Role { Id = Role, Name = "平台管理員" });
    public void Configure(EntityTypeBuilder<RoleGroup> builder) => builder.HasData(new RoleGroup { Id = Group, Name = "平台管理" });
    public void Configure(EntityTypeBuilder<RoleGroupRole> builder) => builder.HasData(new RoleGroupRole { RoleId = Role, GroupId = Group });
}
