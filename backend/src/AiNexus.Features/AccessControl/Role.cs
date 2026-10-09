using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

public sealed class Role
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> role)
    {
        role.ToTable("Roles", "access");
        role.HasKey(x => x.Id);
        role.Property(x => x.Id).HasMaxLength(64);
        role.Property(x => x.Name).HasMaxLength(120);
        role.HasData(new Role { Id = BuiltInAccess.MemberRole, Name = "一般使用者" });
    }
}
