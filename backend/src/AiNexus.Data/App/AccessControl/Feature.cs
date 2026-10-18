using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>
/// 選單入口及其背後的授權。內建資料列由 <c>database/App/Seed</c> 寫入；<see cref="Route"/> 為空代表只是授權、沒有頁面，不會出現在選單中。
/// </summary>
public sealed class Feature
{
    public const int IconMaxLength = 40;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Route { get; set; } = "";
    public int SortOrder { get; set; }
    public string? GroupId { get; set; }
    public string Icon { get; set; } = "";
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
        feature.Property(x => x.Route).HasMaxLength(160);
        feature.Property(x => x.GroupId).HasMaxLength(NavigationGroup.IdMaxLength);
        feature.Property(x => x.Icon).HasMaxLength(Feature.IconMaxLength);
        feature.HasOne<NavigationGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
