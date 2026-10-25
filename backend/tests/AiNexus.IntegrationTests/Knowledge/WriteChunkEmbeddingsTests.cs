using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Knowledge;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Features.Work.Knowledge.Embeddings;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class WriteChunkEmbeddingsTests
{
    private const string Procedure = "knowledge.usp_WriteChunkEmbeddings";

    [Fact]
    public async Task WritesEachDimensionAndSkipsChunksTheProfileAlreadyEmbedded()
    {
        await TestDatabase.WithContextAsync(async db => {
            var sql = new DbContextSqlDatabase<AppDbContext>(db); var vectors = new EmbeddingVectorStore(db, sql);
            var chunks = await SeedChunks(db, 3);
            foreach (var dimensions in VectorDimensions.Supported)
            {
                var profile = await AddProfile(db, dimensions); var other = await AddProfile(db, dimensions);
                var rows = chunks.Select((x, i) => new EmbeddingWrite(x.Id, x.ContentHash, Vector(dimensions, i))).ToArray();
                await vectors.WriteBatchAsync(profile, rows[..2], CancellationToken.None);
                // 已有向量的片段略過、不覆寫：第二批只寫入 rows[2]，rows[0] 仍是第一次的向量。
                await vectors.WriteBatchAsync(profile, [rows[0] with { Vector = Vector(dimensions, 9) }, rows[2]], CancellationToken.None);
                var stored = await vectors.Rows(profile).ToListAsync();
                Assert.Equal(rows.Length, stored.Count);
                foreach (var row in rows)
                {
                    var saved = Assert.Single(stored, x => x.ChunkId == row.ChunkId);
                    Assert.Equal(row.ContentHash, saved.ContentHash); Assert.Equal(row.Vector, saved.Vector.Memory.ToArray());
                }
                // 直接呼叫 procedure 檢查回傳欄位：略過只看同一個 profile，第二次全部略過。
                Assert.Equal(rows.Length, await WriteAsync(sql, other, rows));
                Assert.Equal(0, await WriteAsync(sql, other, rows));
                Assert.Equal(rows.Length, await vectors.Rows(other).CountAsync());
            }
            var unsupported = await Assert.ThrowsAsync<SqlException>(() => sql.QueryProcedureAsync<int>(Procedure, new { ProfileId = 0, Dimensions = 512, Rows = "[]" }));
            Assert.Equal(51000, unsupported.Number);
        });
    }

    [Fact]
    public async Task WritesBatchBeyondTheOldParameterLimitInOneCall()
    {
        await TestDatabase.WithContextAsync(async db => {
            var sql = new DbContextSqlDatabase<AppDbContext>(db); var vectors = new EmbeddingVectorStore(db, sql);
            // 舊寫法每列 3 個參數，800 列＝2400 個，超過 SQL Server 的 2100 上限。
            var chunks = await SeedChunks(db, 800);
            var profile = await AddProfile(db, 1024);
            var rows = chunks.Select((x, i) => new EmbeddingWrite(x.Id, x.ContentHash, Vector(1024, i))).ToArray();
            // 正式流程在工作檢查點的 EF 交易內寫入。
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                await vectors.WriteBatchAsync(profile, rows, CancellationToken.None);
                await tx.CommitAsync();
            }
            Assert.Equal(rows.Length, await vectors.Rows(profile).CountAsync());
            var expected = rows[^1];
            var last = await vectors.Rows(profile).SingleAsync(x => x.ChunkId == expected.ChunkId);
            Assert.Equal(expected.ContentHash, last.ContentHash); Assert.Equal(expected.Vector, last.Vector.Memory.ToArray());
        });
    }

    private static async Task<int> WriteAsync(ISqlDatabase<AppDbContext> sql, EmbeddingProfile profile, IEnumerable<EmbeddingWrite> rows)
    {
        var json = JsonSerializer.Serialize(rows.Select(x => new { chunkId = x.ChunkId, contentHash = Convert.ToHexString(x.ContentHash), vector = x.Vector }));
        return Assert.Single(await sql.QueryProcedureAsync<int>(Procedure, new { ProfileId = profile.Id, profile.Dimensions, Rows = json }));
    }

    // 只用二進位可精確表示的值，比對時不受十進位文字往返的捨入影響。
    private static float[] Vector(int dimensions, int seed)
    {
        var vector = new float[dimensions]; vector[0] = 1; vector[1] = seed * .5f; vector[^1] = -.25f;
        return vector;
    }

    private static async Task<EmbeddingProfile> AddProfile(AppDbContext db, int dimensions)
    {
        var profile = new EmbeddingProfile { Key = $"fixture:{dimensions}:{Guid.NewGuid():N}", Provider = "ollama", Model = "fixture", Dimensions = dimensions, Status = "building" };
        db.Add(profile); await db.SaveChangesAsync();
        return profile;
    }

    private static async Task<Chunk[]> SeedChunks(AppDbContext db, int count)
    {
        var owner = new User { Sid = "embedding-write-fixture", AdAccount = "embedding-write", DisplayName = "測試使用者" };
        var collection = new Resource { OwnerId = owner.Id, Kind = "knowledge", Name = "知識庫" };
        var document = new Resource { OwnerId = owner.Id, Kind = "document", Name = "文件", ParentId = collection.Id };
        db.AddRange(owner, collection, document, new Collection { Id = collection.Id });
        db.Add(new Document { Id = document.Id, CollectionId = collection.Id, FileName = document.Name, ContentType = "text/plain", Status = "ready", PageCount = 1, ChunkCount = count });
        var chunks = Enumerable.Range(0, count).Select(i => new Chunk { DocumentId = document.Id, Ordinal = i, StartPage = 1, EndPage = 1, Text = "片段",
            ContentHash = SHA256.HashData(Encoding.UTF8.GetBytes(i.ToString(CultureInfo.InvariantCulture))), TokenEstimate = 2 }).ToArray();
        db.AddRange(chunks); await db.SaveChangesAsync();
        return chunks;
    }
}
