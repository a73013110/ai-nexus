using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

public sealed class RoleGroupRole
{
    public string RoleId { get; set; } = "";
    public string GroupId { get; set; } = "";
}

internal sealed class RoleGroupRoleConfiguration : IEntityTypeConfiguration<RoleGroupRole>
{
    public void Configure(EntityTypeBuilder<RoleGroupRole> membership)
    {
        membership.ToTable("RoleGroupRoles", "access");
        membership.HasKey(x => new { x.RoleId, x.GroupId });
        membership.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        membership.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        membership.HasData(new RoleGroupRole { RoleId = BuiltInAccess.MemberRole, GroupId = BuiltInAccess.WorkspaceGroup });
    }
}
