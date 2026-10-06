using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Runtime.InteropServices;

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
    public string? Warning { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? JobId { get; set; }
    public string? TextContent { get; set; }
    public int TextVersion { get; set; }
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
    public int SearchId { get; set; }
    public Guid DocumentId { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int Ordinal { get; set; }
    public string HeadingPath { get; set; } = "";
    public string Text { get; set; } = "";
    public byte[] ContentHash { get; set; } = [];
    public int TokenEstimate { get; set; }
}
public sealed class EmbeddingProfile
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int Dimensions { get; set; }
    public string InputFormat { get; set; } = "plain";
    public string QueryInstruction { get; set; } = "";
    public string Revision { get; set; } = "";
    public string ChunkerConfiguration { get; set; } = "";
    public string Status { get; set; } = "building";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}
public sealed class ChunkEmbedding768
{
    public int Id { get; set; }
    public Guid ChunkId { get; set; }
    public int ProfileId { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public SqlVector<float> Vector { get; set; }
}
public sealed class ChunkEmbedding1024
{
    public int Id { get; set; }
    public Guid ChunkId { get; set; }
    public int ProfileId { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public SqlVector<float> Vector { get; set; }
}
public sealed class ConversationKnowledge { public Guid ConversationId { get; set; } public Guid CollectionId { get; set; } }
public sealed class MessageCitation
{
    public Guid MessageId { get; set; }
    public int Number { get; set; }
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public int EndPage { get; set; }
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
}
public sealed record CollectionDto(ResourceDto Resource, string Description, int Documents, int ReadyDocuments);
public sealed record CollectionRequest(string Name, string Description);
public sealed record DocumentDto(Guid Id, Guid? CollectionId, string FileName, string ContentType, string Status, int PageCount, int ChunkCount, string? Warning, Guid? JobId, bool CanEdit, bool HasOriginal, int TextVersion = 0);
public sealed record TextDocumentRequest(string Title, string Text, int? ExpectedVersion = null);
public sealed record TextDocumentDto(Guid Id, string Title, string Text, int Version);
public sealed record DocumentPageDto(int PageNumber, string Text, string Extraction, bool NeedsReview);
public sealed record DocumentJobDto(AiNexus.Modules.Operations.JobDto Job, bool CanControl);
public sealed record CitationDto(int Number, Guid DocumentId, string Title, int PageNumber, string Excerpt, int EndPage = 0);
public sealed record KnowledgeSelectionDto(IReadOnlyList<Guid> CollectionIds);
public sealed record AddDocumentRequest(Guid AttachmentId);
public sealed record KnowledgeSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds);
public sealed record KnowledgeSearchDto(string Mode, IReadOnlyList<KnowledgeHitDto> Hits, long RewriteMs = 0, long EmbedMs = 0, long SearchMs = 0, long RerankMs = 0);
public sealed record KnowledgeHitDto(Guid DocumentId, string Title, int PageNumber, string Text, double Score,
    Guid ChunkId, int EndPage = 0, int Ordinal = 0, int? VectorRank = null, int? FtsRank = null,
    double? VectorScore = null, double? RrfScore = null, double? RerankScore = null, string HeadingPath = "");

public static class KnowledgeConfiguration
{
    public static void Configure(ModelBuilder model, bool sqlite = false)
    {
        var collection = model.Entity<KnowledgeCollection>(); collection.ToTable("Collections", "knowledge"); collection.HasKey(x => x.Id); collection.Property(x => x.Description).HasMaxLength(2000);
        collection.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        var document = model.Entity<KnowledgeDocument>(); document.ToTable("Documents", "knowledge"); document.HasKey(x => x.Id);
        document.Property(x => x.FileName).HasMaxLength(180); document.Property(x => x.ContentType).HasMaxLength(80); document.Property(x => x.Status).HasMaxLength(16); document.Property(x => x.Warning).HasMaxLength(500);
        document.Property(x => x.TextContent).HasMaxLength(200000); document.Property(x => x.TextVersion).IsConcurrencyToken();
        document.HasIndex(x => new { x.CollectionId, x.Status, x.IsDeleted }).IncludeProperties(x => new { x.Id, x.FileName, x.ChunkCount }); document.HasIndex(x => x.AttachmentId);
        document.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        var page = model.Entity<DocumentPage>(); page.ToTable("DocumentPages", "knowledge"); page.HasKey(x => new { x.DocumentId, x.PageNumber }); page.Property(x => x.Extraction).HasMaxLength(16);
        page.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        var chunk = model.Entity<KnowledgeChunk>(); chunk.ToTable("Chunks", "knowledge"); chunk.HasKey(x => x.Id); chunk.Property(x => x.Text).HasMaxLength(4000);
        chunk.Property(x => x.HeadingPath).HasMaxLength(400); chunk.Property(x => x.ContentHash).HasColumnType("binary(32)");
        if (sqlite) chunk.Property(x => x.SearchId).ValueGeneratedNever(); else chunk.Property(x => x.SearchId).UseIdentityColumn();
        chunk.HasIndex(x => x.SearchId).IsUnique();
        chunk.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique(); chunk.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        var profile = model.Entity<EmbeddingProfile>(); profile.ToTable("EmbeddingProfiles", "knowledge"); profile.HasKey(x => x.Id);
        profile.Property(x => x.Key).HasMaxLength(200); profile.HasIndex(x => x.Key).IsUnique();
        profile.Property(x => x.Provider).HasMaxLength(32); profile.Property(x => x.Model).HasMaxLength(160); profile.Property(x => x.InputFormat).HasMaxLength(32);
        profile.Property(x => x.QueryInstruction).HasMaxLength(500); profile.Property(x => x.Revision).HasMaxLength(64); profile.Property(x => x.ChunkerConfiguration).HasMaxLength(500);
        profile.Property(x => x.Status).HasMaxLength(16); profile.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] = 'active'");
        ConfigureVector<ChunkEmbedding768>(model, "ChunkEmbeddings768", 768, sqlite);
        ConfigureVector<ChunkEmbedding1024>(model, "ChunkEmbeddings1024", 1024, sqlite);
        var link = model.Entity<ConversationKnowledge>(); link.ToTable("ConversationCollections", "knowledge"); link.HasKey(x => new { x.ConversationId, x.CollectionId });
        link.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade); link.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        var citation = model.Entity<MessageCitation>(); citation.ToTable("MessageCitations", "knowledge"); citation.HasKey(x => new { x.MessageId, x.Number }); citation.Property(x => x.Title).HasMaxLength(180); citation.Property(x => x.Excerpt).HasMaxLength(800);
        citation.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade); citation.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureVector<T>(ModelBuilder model, string table, int dimensions, bool sqlite) where T : class
    {
        var item = model.Entity<T>(); item.ToTable(table, "knowledge"); item.HasKey("Id").IsClustered();
        item.Property<int>("Id").ValueGeneratedOnAdd(); item.Property<byte[]>("ContentHash").HasColumnType("binary(32)");
        item.HasIndex("ProfileId", "ChunkId").IsUnique(); item.HasIndex("ProfileId", "ContentHash");
        item.HasOne<KnowledgeChunk>().WithMany().HasForeignKey("ChunkId").OnDelete(DeleteBehavior.Cascade);
        item.HasOne<EmbeddingProfile>().WithMany().HasForeignKey("ProfileId").OnDelete(DeleteBehavior.Restrict);
        if (sqlite) item.Property<SqlVector<float>>("Vector").HasConversion(new ValueConverter<SqlVector<float>, byte[]>(v => VectorBytes.Write(v), b => VectorBytes.Read(b))).HasColumnType("BLOB");
        else item.Property<SqlVector<float>>("Vector").HasColumnType($"vector({dimensions})");
    }
}
public static class VectorBytes
{
    public static byte[] Write(SqlVector<float> vector) => MemoryMarshal.AsBytes(vector.Memory.Span).ToArray();
    public static SqlVector<float> Read(byte[] value) => new(MemoryMarshal.Cast<byte, float>(value).ToArray());
}
