using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

[Comment("角色與功能群組的授權關聯。")]
public sealed class RoleGroupRole
{
    [Comment("關聯角色的識別碼。")]
    public string RoleId { get; set; } = "";
    [Comment("關聯功能群組的識別碼。")]
    public string GroupId { get; set; } = "";
}

internal sealed class RoleGroupRoleConfiguration : IEntityTypeConfiguration<RoleGroupRole>
{
    public void Configure(EntityTypeBuilder<RoleGroupRole> membership)
    {
        membership.ToTable("RoleGroupRoles", "accesscontrol");
        membership.HasKey(x => new { x.RoleId, x.GroupId });
        membership.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        membership.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        membership.HasData(new RoleGroupRole { RoleId = BuiltInAccess.MemberRole, GroupId = BuiltInAccess.WorkspaceGroup });
    }
}
