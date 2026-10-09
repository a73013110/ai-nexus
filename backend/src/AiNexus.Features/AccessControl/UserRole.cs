using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

[Comment("使用者與角色的分派關聯。")]
public sealed class UserRole
{
    [Comment("關聯使用者的 Users 主鍵。")]
    public Guid UserId { get; set; }
    [Comment("關聯角色的識別碼。")]
    public string RoleId { get; set; } = "";
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> userRole)
    {
        userRole.ToTable("UserRoles", "accesscontrol");
        userRole.HasKey(x => new { x.UserId, x.RoleId });
        userRole.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
