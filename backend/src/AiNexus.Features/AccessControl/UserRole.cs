using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public string RoleId { get; set; } = "";
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> userRole)
    {
        userRole.ToTable("UserRoles", "access");
        userRole.HasKey(x => new { x.UserId, x.RoleId });
        userRole.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
