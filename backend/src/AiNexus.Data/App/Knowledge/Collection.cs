using AiNexus.Data.App.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>
/// 知識庫本體。名稱、擁有者與分享設定存在同 Id 的 <see cref="Resource"/>；文件是指回此知識庫的 <see cref="Document"/> 資料列。
/// </summary>
public sealed class Collection
{
    public const string Kind = "knowledge";
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
}

internal sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> collection)
    {
        collection.ToTable("Collections", "knowledge");
        collection.HasKey(x => x.Id);
        collection.Property(x => x.Description).HasMaxLength(Collection.DescriptionMaxLength);
        collection.HasOne<Resource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
    }
}
