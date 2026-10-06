using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AiNexus.BuildingBlocks;
using AiNexus.Database;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Knowledge;
using Dapper;
using EDoc.Core.Database.Implementations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST"))) Skip = "未設定 AINEXUS_SQLSERVER_TEST，略過真實 SQL Server 2025 整合測試。"; }
}
public sealed class SqlServerRetrievalTests
{
    [SqlServerFact]
    public async Task NativeVectorsBatchTransactionsDistanceAndProfileIsolation()
    {
        await WithDatabase(async (db, sql, store) => {
            var seed = await Seed(db);
            foreach (var dimensions in VectorDimensions.Supported)
            {
                var active = new EmbeddingProfile { Key = $"fixture:{dimensions}:active", Provider = "ollama", Model = "fixture", Dimensions = dimensions, Status = "building" };
                var building = new EmbeddingProfile { Key = $"fixture:{dimensions}:building", Provider = "ollama", Model = "fixture", Dimensions = dimensions, Status = "building" };
                db.AddRange(active, building); await db.SaveChangesAsync(); var vectors = new EmbeddingVectorStore(db);
                float[] Vector(float a, float b) { var v = new float[dimensions]; v[0] = a; v[1] = b; return v; }
                var values = seed.Chunks.Select((x, i) => new EmbeddingWrite(x.Id, x.ContentHash, i == 0 ? Vector(.8f, .6f) : i == 1 ? Vector(0, 1) : Vector(1, 0))).ToArray();
                await using (var tx = await db.Database.BeginTransactionAsync()) { await vectors.WriteBatchAsync(active, values, CancellationToken.None); await tx.RollbackAsync(); }
                Assert.Empty(await vectors.Rows(active).ToListAsync());
                await using (var tx = await db.Database.BeginTransactionAsync()) {
                    await vectors.WriteBatchAsync(active, values, CancellationToken.None);
                    await vectors.WriteBatchAsync(active, values, CancellationToken.None);
                    await vectors.WriteBatchAsync(building, [new(seed.Chunks[0].Id, seed.Chunks[0].ContentHash, Vector(0, 1)), new(seed.Chunks[1].Id, seed.Chunks[1].ContentHash, Vector(1, 0))], CancellationToken.None);
                    await tx.CommitAsync();
                }
                Assert.Equal(values.Length, await vectors.Rows(active).CountAsync());
                Assert.Equal(dimensions, (await vectors.Rows(active).FirstAsync()).Vector.Memory.Length);
                var search = await store.SearchAsync([seed.Collection], active, "採購", Vector(1, 0), "vector", CancellationToken.None);
                var hit = Assert.Single(search.Hits); Assert.Equal(seed.Chunks[0].Id, hit.ChunkId); Assert.Equal(.8, hit.VectorScore!.Value, 5);
                var next = await store.SearchAsync([seed.Collection], building, "採購", Vector(1, 0), "vector", CancellationToken.None);
                Assert.Equal(seed.Chunks[1].Id, Assert.Single(next.Hits).ChunkId);
                var cached = await vectors.CachedAsync(active, [seed.Chunks[0].ContentHash], CancellationToken.None); Assert.Single(cached);
            }
            Assert.Equal(17, await sql.QuerySingleAsync<int>("SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)"));
        });
    }
    [SqlServerFact]
    public async Task TraditionalChineseFullTextPopulationAndAclBeforeTop()
    {
        await WithDatabase(async (db, sql, store) => {
            var seed = await Seed(db); var profile = new EmbeddingProfile { Key = "fixture:fts", Provider = "none", Dimensions = 1024, Model = "fixture", Status = "active" }; db.Add(profile); await db.SaveChangesAsync();
            Assert.True(await sql.QuerySingleAsync<bool>("SELECT CAST(SERVERPROPERTY('IsFullTextInstalled') AS bit)"));
            Assert.True(await sql.QuerySingleAsync<bool>("SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028) THEN 1 ELSE 0 END AS bit)"));
            var timer = Stopwatch.StartNew(); KnowledgeSearchDto? search = null;
            while (timer.Elapsed < TimeSpan.FromSeconds(90))
            {
                search = await store.SearchAsync([seed.Collection], profile, "採購核准", null, "keyword", CancellationToken.None);
                var populated = await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM FREETEXTTABLE([knowledge].[Chunks], ([Text], [HeadingPath]), @Query, LANGUAGE 1028)", new { Query = "採購核准" });
                if (populated >= 42 && search.Hits.Count > 0) break;
                await Task.Delay(500);
            }
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(90), "全文索引未於 90 秒內完成測試資料 population。");
            var hit = Assert.Single(search!.Hits); Assert.Contains(hit.ChunkId, seed.Chunks.Take(2).Select(x => x.Id)); Assert.Equal(1, hit.FtsRank); Assert.Null(hit.VectorRank);
        });
    }
    private static async Task<(Guid Collection, KnowledgeChunk[] Chunks)> Seed(NexusDbContext db)
    {
        var actor = new NexusUser { Sid = "sql-retrieval-fixture", Account = "fixture", DisplayName = "測試使用者" }; db.Add(actor);
        var allowed = new WorkspaceResource { OwnerId = actor.Id, Kind = "knowledge", Name = "授權知識庫" }; var denied = new WorkspaceResource { OwnerId = actor.Id, Kind = "knowledge", Name = "範圍外知識庫" };
        db.AddRange(allowed, denied); db.AddRange(new KnowledgeCollection { Id = allowed.Id }, new KnowledgeCollection { Id = denied.Id });
        var docs = new[] { new WorkspaceResource { OwnerId = actor.Id, Kind = "document", Name = "授權文件", ParentId = allowed.Id }, new WorkspaceResource { OwnerId = actor.Id, Kind = "document", Name = "範圍外文件", ParentId = denied.Id } };
        db.AddRange(docs);
        for (var i = 0; i < docs.Length; i++) db.Add(new KnowledgeDocument { Id = docs[i].Id, CollectionId = i == 0 ? allowed.Id : denied.Id, FileName = docs[i].Name, ContentType = "text/plain", Status = "ready", PageCount = 1, ChunkCount = i == 0 ? 2 : 50 });
        var chunks = Enumerable.Range(0, 52).Select(i => new KnowledgeChunk { DocumentId = i < 2 ? docs[0].Id : docs[1].Id, Ordinal = i < 2 ? i : i - 2, StartPage = 1, EndPage = 1, HeadingPath = "第三章 › 採購核准", Text = i < 2 ? "採購核准後，由主管簽署再付款。" : string.Join('。', Enumerable.Repeat("採購核准採購核准", 15)), ContentHash = SHA256.HashData(Encoding.UTF8.GetBytes(i.ToString())), TokenEstimate = 30 }).ToArray();
        db.AddRange(chunks); await db.SaveChangesAsync(); return (allowed.Id, chunks);
    }
    private static async Task WithDatabase(Func<NexusDbContext, EDoc.Core.Database.Interfaces.IDbHelper<INexusDatabase>, SqlServerRetrievalStore, Task> test)
    {
        var settings = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST")!) { InitialCatalog = "master" };
        var name = "AINexus_Retrieval_Test_" + Guid.NewGuid().ToString("N"); var created = false;
        await using var master = new SqlConnection(settings.ConnectionString); await master.OpenAsync();
        try
        {
            await master.ExecuteAsync($"CREATE DATABASE [{name}]"); created = true; settings.InitialCatalog = name;
            await using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer(settings.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Nexus"] = settings.ConnectionString }).Build();
            var sql = new DbHelper<INexusDatabase>(new NexusConnectionFactory(config)); using var cache = new MemoryCache(new MemoryCacheOptions());
            var store = new SqlServerRetrievalStore(sql, Options.Create(new KnowledgeOptions { VectorCandidates = 1, FtsCandidates = 1, RerankCandidates = 1, TopK = 1 }), cache, NullLogger<SqlServerRetrievalStore>.Instance);
            await test(db, sql, store);
        }
        finally
        {
            if (created) { SqlConnection.ClearAllPools(); await master.ExecuteAsync($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]"); }
        }
    }
}
