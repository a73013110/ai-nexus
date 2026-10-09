using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>
/// A shared knowledge collection. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same
/// id; its documents are <see cref="KnowledgeDocument"/> rows that point back to it.
/// </summary>
public sealed class KnowledgeCollection
{
    public const string Kind = "knowledge";
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
}

internal sealed class KnowledgeCollectionConfiguration : IEntityTypeConfiguration<KnowledgeCollection>
{
    public void Configure(EntityTypeBuilder<KnowledgeCollection> collection)
    {
        collection.ToTable("Collections", "knowledge"); collection.HasKey(x => x.Id); collection.Property(x => x.Description).HasMaxLength(KnowledgeCollection.DescriptionMaxLength);
    }
}
