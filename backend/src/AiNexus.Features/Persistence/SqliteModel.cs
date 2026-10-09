using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Indexing;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Every model difference for SQLite, which only the relational integration tests use. Entity configurations describe
/// SQL Server; this runs after them and overrides what SQLite cannot store or generate.
/// </summary>
internal static class SqliteModel
{
    public const string ProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    public static void Configure(ModelBuilder model)
    {
        // No identity on a non-key column: the indexer numbers chunks itself.
        model.Entity<KnowledgeChunk>().Property(x => x.SearchId).ValueGeneratedNever();
        // No native vector type: vectors are stored as float blobs.
        var vector = new ValueConverter<SqlVector<float>, byte[]>(v => VectorBytes.Write(v), b => VectorBytes.Read(b));
        foreach (var embedding in new[] { typeof(ChunkEmbedding768), typeof(ChunkEmbedding1024) })
            model.Entity(embedding).Property<SqlVector<float>>("Vector").HasConversion(vector).HasColumnType("BLOB");
        // No native offset ordering: DateTimeOffset is stored as a sortable integer.
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
    }
}
