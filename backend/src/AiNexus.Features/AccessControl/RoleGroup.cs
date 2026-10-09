using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

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
        group.ToTable("RoleGroups", "access");
        group.HasKey(x => x.Id);
        group.Property(x => x.Id).HasMaxLength(64);
        group.Property(x => x.Name).HasMaxLength(120);
        group.HasData(new RoleGroup { Id = BuiltInAccess.WorkspaceGroup, Name = "基本工作區" });
    }
}
