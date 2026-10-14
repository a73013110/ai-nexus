using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl.Navigation;

/// <summary>
/// A heading of the workspace menu. Groups nest through <see cref="ParentId"/> and only arrange features; they grant nothing.
/// The built-in groups are seeded here; a module that needs its own group seeds it next to its <see cref="FeatureSeed"/>.
/// </summary>
[Comment("工作區選單的分群；可巢狀，只決定功能入口的排列，不影響授權。")]
public sealed class NavigationGroup
{
    public const int IdMaxLength = 64, NameMaxLength = 120;

    [Comment("資料的主鍵識別碼。")]
    public string Id { get; set; } = "";
    [Comment("上層分群；null 表示位於選單最上層。")]
    public string? ParentId { get; set; }
    [Comment("業務物件的顯示名稱。")]
    public string Name { get; set; } = "";
    [Comment("同一層內的顯示順序；數值越小越前，同一層的功能排在分群之前。")]
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
        group.HasData(
            new NavigationGroup { Id = BuiltInAccess.WorkNavigation, Name = "AI 工作台", SortOrder = 10 },
            new NavigationGroup { Id = BuiltInAccess.CollaborationNavigation, Name = "協作", SortOrder = 20 },
            new NavigationGroup { Id = BuiltInAccess.SystemNavigation, Name = "系統管理", SortOrder = 30 });
    }
}
