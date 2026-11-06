using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

public sealed class Role : IRowVersioned
{
    public const int IdMaxLength = 64;
    public string Id { get; set; } = "";
    public long RowVersion { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> role)
    {
        role.ToTable("Roles", "accesscontrol");
        role.HasKey(x => x.Id);
        role.Property(x => x.RowVersion).IsRowVersion().HasConversion<byte[]>();
        role.Property(x => x.Id).HasMaxLength(Role.IdMaxLength);
        role.Property(x => x.Name).HasMaxLength(120);
    }
}
