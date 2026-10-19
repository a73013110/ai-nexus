using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Organization;

/// <summary>
/// 組織單位（部門）樹的一個節點；只用來計算資料範圍與主管關係，不授予功能（ADR 0017）。
/// 同層名稱唯一由資料庫擋下；不可成環與深度上限由程式檢查。
/// </summary>
public sealed class Unit
{
    public const int NameMaxLength = 120;

    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
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
