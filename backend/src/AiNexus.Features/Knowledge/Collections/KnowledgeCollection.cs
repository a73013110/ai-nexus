using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>
/// 知識庫本體。名稱、擁有者與分享設定存在同 Id 的 <see cref="WorkspaceResource"/>；文件是指回此知識庫的 <see cref="KnowledgeDocument"/> 資料列。
/// </summary>
[Comment("知識庫的資料資源關聯及索引資訊。")]
public sealed class KnowledgeCollection
{
    public const string Kind = "knowledge";
    public const int DescriptionMaxLength = 2000;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("業務物件的用途說明。")]
    public string Description { get; set; } = "";
}

internal sealed class KnowledgeCollectionConfiguration : IEntityTypeConfiguration<KnowledgeCollection>
{
    public void Configure(EntityTypeBuilder<KnowledgeCollection> collection)
    {
        collection.ToTable("Collections", "knowledge"); collection.HasKey(x => x.Id); collection.Property(x => x.Description).HasMaxLength(KnowledgeCollection.DescriptionMaxLength);
    }
}
