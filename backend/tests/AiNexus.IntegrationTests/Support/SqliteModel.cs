using System.Data.Common;
using System.Runtime.InteropServices;
using AiNexus.Data.App.Knowledge;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 讓 SQL Server 的 <c>AppDbContext</c> 模型能在 SQLite 測試資料庫上建立與查詢。只存在測試組件：正式模型不需要知道 SQLite。
/// </summary>
/// <remarks>
/// 覆寫必須在所有實體組態套用之後執行，才能蓋過原本的設定：
/// SQLite 不允許非主鍵欄位設定 Identity，切塊編號改由索引器自行管理（<c>ValueGeneratedNever</c>）；
/// SQLite 沒有原生向量型別，改存為 BLOB；
/// SQLite 無法對 <see cref="DateTimeOffset"/> 的 ISO 字串做時區無關的精確排序，改以 <see cref="DateTimeOffsetToBinaryConverter"/> 存成可排序的整數。
/// SQLite 測試改用 LocalDB（導入計畫階段 3）後刪除這個檔案。
/// </remarks>
public static class SqliteModel
{
    /// <summary>以 SQLite 開啟 <paramref name="connectionString"/>，並套用上述模型差異。</summary>
    public static DbContextOptionsBuilder UseTestSqlite(this DbContextOptionsBuilder options, string connectionString)
        => options.UseSqlite(connectionString).ReplaceService<IModelCustomizer, Customizer>();

    /// <inheritdoc cref="UseTestSqlite(DbContextOptionsBuilder, string)"/>
    public static DbContextOptionsBuilder<T> UseTestSqlite<T>(this DbContextOptionsBuilder<T> options, string connectionString) where T : DbContext
        => options.UseSqlite(connectionString).ReplaceService<IModelCustomizer, Customizer>();

    /// <summary>以 SQLite 開啟既有連線（例如 in-memory），並套用上述模型差異。</summary>
    public static DbContextOptionsBuilder<T> UseTestSqlite<T>(this DbContextOptionsBuilder<T> options, DbConnection connection) where T : DbContext
        => options.UseSqlite(connection).ReplaceService<IModelCustomizer, Customizer>();

    // 值轉換器是運算式樹，不能直接用 Span，所以包成方法。
    private static byte[] WriteVector(SqlVector<float> vector) => MemoryMarshal.AsBytes(vector.Memory.Span).ToArray();
    private static SqlVector<float> ReadVector(byte[] value) => new(MemoryMarshal.Cast<byte, float>(value).ToArray());

    private sealed class Customizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder model, DbContext context)
        {
            base.Customize(model, context);
            model.Entity<Chunk>().Property(x => x.SearchId).ValueGeneratedNever();

            var vector = new ValueConverter<SqlVector<float>, byte[]>(v => WriteVector(v), b => ReadVector(b));
            foreach (var embedding in new[] { typeof(ChunkEmbedding768), typeof(ChunkEmbedding1024) })
            {
                model.Entity(embedding).Property<SqlVector<float>>("Vector").HasConversion(vector).HasColumnType("BLOB");
            }

            foreach (var property in model.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }
}
