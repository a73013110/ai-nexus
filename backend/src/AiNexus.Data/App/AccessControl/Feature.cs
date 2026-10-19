using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>
/// 授權用的功能列：啟用狀態、名稱覆寫與現行選單位置。頁面網址、預設名稱與圖示由程式的 FeatureCatalog 提供，
/// 內建資料列由 <c>database/App/Seed</c> 寫入。<see cref="Name"/> 為 null 代表使用目錄的預設名稱。
/// </summary>
public sealed class Feature
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public int SortOrder { get; set; }
    public string? GroupId { get; set; }
    public bool Enabled { get; set; } = true;
}

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> feature)
    {
        feature.ToTable("Features", "accesscontrol");
        feature.HasKey(x => x.Id);
        feature.Property(x => x.Id).HasMaxLength(64);
        feature.Property(x => x.Name).HasMaxLength(120);
        feature.Property(x => x.GroupId).HasMaxLength(NavigationGroup.IdMaxLength);
        feature.HasOne<NavigationGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
