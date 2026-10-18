using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

public sealed class RoleGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

internal sealed class RoleGroupConfiguration : IEntityTypeConfiguration<RoleGroup>
{
    public void Configure(EntityTypeBuilder<RoleGroup> group)
    {
        group.ToTable("RoleGroups", "accesscontrol");
        group.HasKey(x => x.Id);
        group.Property(x => x.Id).HasMaxLength(64);
        group.Property(x => x.Name).HasMaxLength(120);
    }
}
