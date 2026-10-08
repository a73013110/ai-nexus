using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Runtime.InteropServices;

namespace AiNexus.Features.Knowledge;

/// <summary>One indexed passage of a collection document; <see cref="SearchId"/> is the full-text key.</summary>
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

public sealed record KnowledgeSearchDto(string Mode, IReadOnlyList<KnowledgeHitDto> Hits, long RewriteMs = 0, long EmbedMs = 0, long SearchMs = 0, long RerankMs = 0);
public sealed record KnowledgeHitDto(Guid DocumentId, string Title, int PageNumber, string Text, double Score,
    Guid ChunkId, int EndPage = 0, int Ordinal = 0, int? VectorRank = null, int? FtsRank = null,
    double? VectorScore = null, double? RrfScore = null, double? RerankScore = null, string HeadingPath = "");

public static class VectorBytes
{
    public static byte[] Write(SqlVector<float> vector) => MemoryMarshal.AsBytes(vector.Memory.Span).ToArray();
    public static SqlVector<float> Read(byte[] value) => new(MemoryMarshal.Cast<byte, float>(value).ToArray());
}

internal sealed class KnowledgeChunkConfiguration(bool sqlite) : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> chunk)
    {
        chunk.ToTable("Chunks", "knowledge"); chunk.HasKey(x => x.Id); chunk.Property(x => x.Text).HasMaxLength(4000);
        chunk.Property(x => x.HeadingPath).HasMaxLength(400); chunk.Property(x => x.ContentHash).HasColumnType("binary(32)");
        if (sqlite) chunk.Property(x => x.SearchId).ValueGeneratedNever(); else chunk.Property(x => x.SearchId).UseIdentityColumn();
        chunk.HasIndex(x => x.SearchId).IsUnique();
        chunk.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique(); chunk.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EmbeddingProfileConfiguration : IEntityTypeConfiguration<EmbeddingProfile>
{
    public void Configure(EntityTypeBuilder<EmbeddingProfile> profile)
    {
        profile.ToTable("EmbeddingProfiles", "knowledge"); profile.HasKey(x => x.Id);
        profile.Property(x => x.Key).HasMaxLength(200); profile.HasIndex(x => x.Key).IsUnique();
        profile.Property(x => x.Provider).HasMaxLength(32); profile.Property(x => x.Model).HasMaxLength(160); profile.Property(x => x.InputFormat).HasMaxLength(32);
        profile.Property(x => x.QueryInstruction).HasMaxLength(500); profile.Property(x => x.Revision).HasMaxLength(64); profile.Property(x => x.ChunkerConfiguration).HasMaxLength(500);
        profile.Property(x => x.Status).HasMaxLength(16); profile.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] = 'active'");
    }
}

/// <summary>One vector table per supported dimension; SQLite test databases store the vector as a blob.</summary>
internal sealed class ChunkEmbeddingConfiguration<T>(string table, int dimensions, bool sqlite) : IEntityTypeConfiguration<T> where T : class
{
    public void Configure(EntityTypeBuilder<T> item)
    {
        item.ToTable(table, "knowledge"); item.HasKey("Id").IsClustered();
        item.Property<int>("Id").ValueGeneratedOnAdd(); item.Property<byte[]>("ContentHash").HasColumnType("binary(32)");
        item.HasIndex("ProfileId", "ChunkId").IsUnique(); item.HasIndex("ProfileId", "ContentHash");
        item.HasOne<KnowledgeChunk>().WithMany().HasForeignKey("ChunkId").OnDelete(DeleteBehavior.Cascade);
        item.HasOne<EmbeddingProfile>().WithMany().HasForeignKey("ProfileId").OnDelete(DeleteBehavior.Restrict);
        if (sqlite) item.Property<SqlVector<float>>("Vector").HasConversion(new ValueConverter<SqlVector<float>, byte[]>(v => VectorBytes.Write(v), b => VectorBytes.Read(b))).HasColumnType("BLOB");
        else item.Property<SqlVector<float>>("Vector").HasColumnType($"vector({dimensions})");
    }
}
