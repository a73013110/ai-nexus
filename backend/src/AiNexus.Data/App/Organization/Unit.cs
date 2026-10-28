using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Organization;

/// <summary>
/// 組織單位（部門）樹的一個節點；只用來計算資料範圍與主管關係，不授予功能（ADR 0017）。
/// 同層名稱唯一由資料庫擋下；不可成環與深度上限由程式檢查。
/// <see cref="GdDepNo"/> 有值的單位來自公文系統（GD）的部門，重複匯入以它對應（唯一）；null 是管理員手建的單位，匯入不會動到。
/// <see cref="HeadRank"/> 有值的是上層的首長單位（ADR 0024），本地設定，匯入不覆寫。
/// </summary>
public sealed class Unit
{
    public const int NameMaxLength = 120;
    public const byte MaxHeadRank = 9;

    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    public int? GdDepNo { get; set; }
    public byte? HeadRank { get; set; }
}

internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> unit)
    {
        unit.ToTable("Units", "organization");
        unit.HasKey(x => x.Id);
        unit.Property(x => x.Name).HasMaxLength(Unit.NameMaxLength);
        unit.HasOne<Unit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
