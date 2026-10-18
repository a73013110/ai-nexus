using AiNexus.Data.App.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Collaboration;

public sealed class ResourceGroup
{
    public Guid ResourceId { get; set; }
    public string GroupId { get; set; } = "";
}

internal sealed class ResourceGroupConfiguration : IEntityTypeConfiguration<ResourceGroup>
{
    public void Configure(EntityTypeBuilder<ResourceGroup> group)
    {
        group.ToTable("ResourceGroups", "collaboration");
        group.HasKey(x => new { x.ResourceId, x.GroupId });
        group.Property(x => x.GroupId).HasMaxLength(64);
        group.HasOne<Resource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        group.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
