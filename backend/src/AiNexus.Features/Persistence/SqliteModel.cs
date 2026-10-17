using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Indexing;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiNexus.Features.Persistence;

/// <summary>
/// SQLite 整合測試模型差異轉換器。
/// </summary>
/// <remarks>
/// 正式環境是 SQL Server，整合測試則用 SQLite In-Memory 驗證關聯行為。下列覆寫必須在所有實體配置套用之後執行，才能蓋過原本的設定：
/// SQLite 不允許非主鍵欄位設定 Identity，切塊編號改由索引器自行管理（<c>ValueGeneratedNever</c>）；
/// SQLite 沒有原生向量型別，改存為 BLOB；
/// SQLite 無法對 <see cref="DateTimeOffset"/> 的 ISO 字串做時區無關的精確排序，改以 <see cref="DateTimeOffsetToBinaryConverter"/> 存成可排序的整數。
/// </remarks>
internal static class SqliteModel
{
    public const string ProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    public static void Configure(ModelBuilder model)
    {
        model.Entity<KnowledgeChunk>().Property(x => x.SearchId).ValueGeneratedNever();

        var vector = new ValueConverter<SqlVector<float>, byte[]>(
            v => VectorBytes.Write(v),
            b => VectorBytes.Read(b));

        foreach (var embedding in new[] { typeof(ChunkEmbedding768), typeof(ChunkEmbedding1024) })
        {
            model.Entity(embedding).Property<SqlVector<float>>("Vector")
                .HasConversion(vector)
                .HasColumnType("BLOB");
        }

        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }
}
