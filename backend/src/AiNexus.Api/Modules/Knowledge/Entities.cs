using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Knowledge;

public sealed class KnowledgeCollection { public Guid Id { get; set; } public string Description { get; set; } = ""; }
public sealed class KnowledgeDocument
{
    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string? EmbeddingProfile { get; set; }
    public string? Warning { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? JobId { get; set; }
}
public sealed class DocumentPage
{
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public string Text { get; set; } = "";
    public string Extraction { get; set; } = "native";
    public bool NeedsReview { get; set; }
}
public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = "";
    public string? EmbeddingJson { get; set; }
    public string? EmbeddingProfile { get; set; }
}
public sealed class ConversationKnowledge { public Guid ConversationId { get; set; } public Guid CollectionId { get; set; } }
public sealed class MessageCitation
{
    public Guid MessageId { get; set; }
    public int Number { get; set; }
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
}
public sealed record CollectionDto(ResourceDto Resource, string Description, int Documents, int ReadyDocuments);
public sealed record CollectionRequest(string Name, string Description);
public sealed record DocumentDto(Guid Id, Guid? CollectionId, string FileName, string ContentType, string Status, int PageCount, int ChunkCount, string? Warning, Guid? JobId, bool CanEdit, bool HasOriginal);
public sealed record DocumentPageDto(int PageNumber, string Text, string Extraction, bool NeedsReview);
public sealed record DocumentJobDto(AiNexus.Modules.Operations.JobDto Job, bool CanControl);
public sealed record CitationDto(int Number, Guid DocumentId, string Title, int PageNumber, string Excerpt);
public sealed record KnowledgeSelectionDto(IReadOnlyList<Guid> CollectionIds);
public sealed record AddDocumentRequest(Guid AttachmentId);
public sealed record KnowledgeSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds);
public sealed record KnowledgeSearchDto(string Mode, IReadOnlyList<KnowledgeHitDto> Hits);
public sealed record KnowledgeHitDto(Guid DocumentId, string Title, int PageNumber, string Text, double Score);

public static class KnowledgeConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var collection = model.Entity<KnowledgeCollection>(); collection.ToTable("Collections", "knowledge"); collection.HasKey(x => x.Id); collection.Property(x => x.Description).HasMaxLength(2000);
        collection.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        var document = model.Entity<KnowledgeDocument>(); document.ToTable("Documents", "knowledge"); document.HasKey(x => x.Id);
        document.Property(x => x.FileName).HasMaxLength(180); document.Property(x => x.ContentType).HasMaxLength(80); document.Property(x => x.Status).HasMaxLength(16); document.Property(x => x.Warning).HasMaxLength(500); document.Property(x => x.EmbeddingProfile).HasMaxLength(200);
        document.HasIndex(x => new { x.CollectionId, x.Status }); document.HasIndex(x => x.AttachmentId);
        document.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        var page = model.Entity<DocumentPage>(); page.ToTable("DocumentPages", "knowledge"); page.HasKey(x => new { x.DocumentId, x.PageNumber }); page.Property(x => x.Extraction).HasMaxLength(16);
        page.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        var chunk = model.Entity<KnowledgeChunk>(); chunk.ToTable("Chunks", "knowledge"); chunk.HasKey(x => x.Id); chunk.Property(x => x.Text).HasMaxLength(2000); chunk.Property(x => x.EmbeddingProfile).HasMaxLength(200);
        chunk.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique(); chunk.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        var link = model.Entity<ConversationKnowledge>(); link.ToTable("ConversationCollections", "knowledge"); link.HasKey(x => new { x.ConversationId, x.CollectionId });
        link.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade); link.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        var citation = model.Entity<MessageCitation>(); citation.ToTable("MessageCitations", "knowledge"); citation.HasKey(x => new { x.MessageId, x.Number }); citation.Property(x => x.Title).HasMaxLength(180); citation.Property(x => x.Excerpt).HasMaxLength(800);
        citation.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade); citation.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
