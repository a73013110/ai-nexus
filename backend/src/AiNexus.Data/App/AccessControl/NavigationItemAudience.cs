using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>
/// 外部連結的可見功能群組；連結沒有任何列時所有登入者可見。外鍵都是 NO ACTION：刪除節點或功能群組時，程式要先刪這裡的列。
/// </summary>
public sealed class NavigationItemAudience
{
    public string NavigationItemId { get; set; } = "";
    public string GroupId { get; set; } = "";
}

internal sealed class NavigationItemAudienceConfiguration : IEntityTypeConfiguration<NavigationItemAudience>
{
    public void Configure(EntityTypeBuilder<NavigationItemAudience> audience)
    {
        audience.ToTable("NavigationItemAudiences", "accesscontrol");
        audience.HasKey(x => new { x.NavigationItemId, x.GroupId });
        audience.HasOne<NavigationItem>().WithMany().HasForeignKey(x => x.NavigationItemId).OnDelete(DeleteBehavior.Restrict);
        audience.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
