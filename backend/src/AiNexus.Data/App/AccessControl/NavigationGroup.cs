using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>
/// 工作區選單的一個標題。透過 <see cref="ParentId"/> 巢狀，只負責排列功能，不授予任何權限。
/// </summary>
public sealed class NavigationGroup
{
    public const int IdMaxLength = 64, NameMaxLength = 120;

    public string Id { get; set; } = "";
    public string? ParentId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

internal sealed class NavigationGroupConfiguration : IEntityTypeConfiguration<NavigationGroup>
{
    public void Configure(EntityTypeBuilder<NavigationGroup> group)
    {
        group.ToTable("NavigationGroups", "accesscontrol");
        group.HasKey(x => x.Id);
        group.Property(x => x.Id).HasMaxLength(NavigationGroup.IdMaxLength);
        group.Property(x => x.ParentId).HasMaxLength(NavigationGroup.IdMaxLength);
        group.Property(x => x.Name).HasMaxLength(NavigationGroup.NameMaxLength);
        group.HasOne<NavigationGroup>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
