using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>A navigation entry and the grant behind it. Rows are seeded by each owning module's <see cref="FeatureSeed"/>.</summary>
[Comment("模組註冊的功能入口、顯示名稱、路由及啟用狀態。")]
public sealed class Feature
{
    [Comment("資料的主鍵識別碼。")]
    public string Id { get; set; } = "";
    [Comment("業務物件的顯示名稱。")]
    public string Name { get; set; } = "";
    [Comment("功能入口的本站路由；API 授權仍由後端政策判定。")]
    public string Route { get; set; } = "";
    [Comment("介面顯示順序；數值越小越前。")]
    public int SortOrder { get; set; }
    [Comment("是否啟用；停用不刪除歷史資料。")]
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
    }
}
