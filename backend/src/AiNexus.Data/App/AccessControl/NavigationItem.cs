using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>
/// 工作區選單的一個節點：分群（group）、功能頁面（page）或外部連結（link）。只決定入口的排列與外部連結的可見範圍，不授予任何權限。
/// 各種類的必填／必空欄位由資料庫的 check 約束擋下；父節點必須是分群、不可成環、深度上限由程式檢查。
/// </summary>
public sealed class NavigationItem
{
    public const int IdMaxLength = 64, NameMaxLength = 120, UrlMaxLength = 2048, IconMaxLength = 50;
    public const string GroupKind = "group", PageKind = "page", LinkKind = "link";

    public string Id { get; set; } = "";
    public string? ParentId { get; set; }
    public string Kind { get; set; } = "";
    public string? FeatureId { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public bool OpenInNewTab { get; set; } = true;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class NavigationItemConfiguration : IEntityTypeConfiguration<NavigationItem>
{
    public void Configure(EntityTypeBuilder<NavigationItem> item)
    {
        item.ToTable("NavigationItems", "accesscontrol");
        item.HasKey(x => x.Id);
        item.Property(x => x.Id).HasMaxLength(NavigationItem.IdMaxLength);
        item.Property(x => x.ParentId).HasMaxLength(NavigationItem.IdMaxLength);
        item.Property(x => x.Kind).HasMaxLength(10);
        item.Property(x => x.FeatureId).HasMaxLength(64);
        item.Property(x => x.Name).HasMaxLength(NavigationItem.NameMaxLength);
        item.Property(x => x.Url).HasMaxLength(NavigationItem.UrlMaxLength);
        item.Property(x => x.Icon).HasMaxLength(NavigationItem.IconMaxLength);
        item.HasOne<NavigationItem>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<Feature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
    }
}
