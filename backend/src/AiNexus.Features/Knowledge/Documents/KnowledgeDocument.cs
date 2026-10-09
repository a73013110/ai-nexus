using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Collections;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// A source document: in a collection (<see cref="CollectionId"/>), or a standalone document of a conversation or project.
/// Its name, owner and project live on the <see cref="WorkspaceResource"/> with the same id. Text sources keep their
/// editable text and a <see cref="TextVersion"/> that guards concurrent edits.
/// </summary>
public sealed class KnowledgeDocument
{
    public const string Kind = "document";

    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string? Warning { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? JobId { get; set; }
    public string? TextContent { get; set; }
    public int TextVersion { get; set; }
}

internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> document)
    {
        document.ToTable("Documents", "knowledge"); document.HasKey(x => x.Id);
        document.Property(x => x.FileName).HasMaxLength(180); document.Property(x => x.ContentType).HasMaxLength(80); document.Property(x => x.Status).HasMaxLength(16); document.Property(x => x.Warning).HasMaxLength(500);
        document.Property(x => x.TextContent).HasMaxLength(200000); document.Property(x => x.TextVersion).IsConcurrencyToken();
        document.HasIndex(x => new { x.CollectionId, x.Status, x.IsDeleted }).IncludeProperties(x => new { x.Id, x.FileName, x.ChunkCount }); document.HasIndex(x => x.AttachmentId);
        document.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
