using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>
/// A shared knowledge collection. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same
/// id; its documents are <see cref="KnowledgeDocument"/> rows that point back to it.
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
