using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Tests;

public sealed class SqlServerRetrievalTests
{
    [Fact]
    public async Task FullTextRuntimeFailureFallsBackToVectorAndExplainsKeywordUnavailability()
    {
        await WithDatabase(async (db, sql, _) => {
            var seed = await Seed(db);
            var profile = new EmbeddingProfile { Key = "fixture:fts-failure", Provider = "ollama", Model = "fixture", Dimensions = 1024, Status = "active" };
            db.Add(profile); await db.SaveChangesAsync();
            var vector = new float[1024]; vector[0] = 1;
            var vectors = new EmbeddingVectorStore(db, sql);
            await vectors.WriteBatchAsync(profile, [new(seed.Chunks[0].Id, seed.Chunks[0].ContentHash, vector)], CancellationToken.None);
            var failing = new FullTextFailure(sql);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var store = new SqlServerRetrievalStore(failing, Options.Create(new KnowledgeOptions()), cache, NullLogger<SqlServerRetrievalStore>.Instance);
            var result = await store.SearchAsync([seed.Collection], profile, "採購核准", vector, "hybrid", CancellationToken.None);
            Assert.Equal("vector", result.Mode); Assert.Equal(seed.Chunks[0].Id, Assert.Single(result.Hits).ChunkId);
            Assert.Equal(1, failing.Failures);
            var error = await Assert.ThrowsAsync<ApiException>(() => store.SearchAsync([seed.Collection], profile, "採購核准", null, "keyword", CancellationToken.None));
            Assert.Equal("fulltext_unavailable", error.Code); Assert.Equal(1, failing.Failures);
        });
    }

    private sealed class FullTextFailure(ISqlDatabase<NexusDbContext> inner) : ISqlDatabase<NexusDbContext>
    {
        public int Failures { get; private set; }
        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.QueryAsync<T>(Rewrite(sql), parameters, commandTimeout, cancellationToken);
        public Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.QuerySingleAsync<T>(Rewrite(sql), parameters, commandTimeout, cancellationToken);
        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.QuerySingleOrDefaultAsync<T>(Rewrite(sql), parameters, commandTimeout, cancellationToken);
        public Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.ExecuteAsync(Rewrite(sql), parameters, commandTimeout, cancellationToken);
        private string Rewrite(string sql)
        {
            if (!sql.Contains("FREETEXTTABLE", StringComparison.Ordinal)) return sql;
            // Raise a real SqlException without depending on a broken server installation.
            Failures++; return "RAISERROR (30053, 16, 1)";
        }
    }

    [Fact]
    public async Task NativeVectorsBatchTransactionsDistanceAndProfileIsolation()
    {
        await WithDatabase(async (db, sql, store) => {
            var seed = await Seed(db);
            foreach (var dimensions in VectorDimensions.Supported)
            {
                var active = new EmbeddingProfile { Key = $"fixture:{dimensions}:active", Provider = "ollama", Model = "fixture", Dimensions = dimensions, Status = "building" };
                var building = new EmbeddingProfile { Key = $"fixture:{dimensions}:building", Provider = "ollama", Model = "fixture", Dimensions = dimensions, Status = "building" };
                db.AddRange(active, building); await db.SaveChangesAsync(); var vectors = new EmbeddingVectorStore(db, sql);
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
    [Fact]
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
    [Fact]
    public async Task DiagnosticBulkImportIsIdempotentIndependentAndRetentionIsBounded()
    {
        await WithDatabase(async (db, _, _) => {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddDbContext<NexusDbContext>(o => o.UseSqlServer(db.Database.GetConnectionString()!));
            using var provider = services.BuildServiceProvider(); using var health = new AiNexus.Platform.Diagnostics.DiagnosticHealth();
            var options = Options.Create(new AiNexus.Platform.Diagnostics.DiagnosticOptions { CleanupBatchSize = 2 });
            var store = new AiNexus.Features.Diagnostics.DiagnosticStore(provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(), options, health);
            var code = AiNexus.Platform.Diagnostics.Issues.NewCode();
            var row = new AiNexus.Platform.Diagnostics.DiagnosticEvent { IssueCode = code, Level = Microsoft.Extensions.Logging.LogLevel.Error, At = DateTimeOffset.UtcNow, TraceId = new string('a', 32) };
            var user = new NexusUser { Sid = "diagnostic-rollback", Account = "rollback", DisplayName = "fixture" };
            await using (var transaction = await db.Database.BeginTransactionAsync()) {
                db.Add(user); await db.SaveChangesAsync(); await store.WriteAsync([row, row], CancellationToken.None); await transaction.RollbackAsync();
            }
            await store.WriteAsync([row], CancellationToken.None);
            Assert.False(await db.Users.AsNoTracking().AnyAsync(x => x.Id == user.Id));
            Assert.Equal(1, await db.Set<AiNexus.Platform.Diagnostics.DiagnosticEvent>().CountAsync(x => x.IssueCode == code));
            Assert.Equal(row.TraceId, (await db.Set<AiNexus.Platform.Diagnostics.DiagnosticEvent>().AsNoTracking().SingleAsync(x => x.LogId == row.LogId)).TraceId);
            await store.WriteAsync(Enumerable.Range(0, 5).Select(_ => new AiNexus.Platform.Diagnostics.DiagnosticEvent { At = DateTimeOffset.UtcNow.AddDays(-31) }).ToArray(), CancellationToken.None);
            await store.CleanupAsync(CancellationToken.None);
            Assert.Equal(1, await db.Set<AiNexus.Platform.Diagnostics.DiagnosticEvent>().CountAsync());
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
        var chunks = Enumerable.Range(0, 52).Select(i => new KnowledgeChunk { DocumentId = i < 2 ? docs[0].Id : docs[1].Id, Ordinal = i < 2 ? i : i - 2, StartPage = 1, EndPage = 1, HeadingPath = "第三章 › 採購核准", Text = i < 2 ? "採購核准後，由主管簽署再付款。" : string.Join('。', Enumerable.Repeat("採購核准採購核准", 15)), ContentHash = SHA256.HashData(Encoding.UTF8.GetBytes(i.ToString(CultureInfo.InvariantCulture))), TokenEstimate = 30 }).ToArray();
        db.AddRange(chunks); await db.SaveChangesAsync(); return (allowed.Id, chunks);
    }
    private static async Task WithDatabase(Func<NexusDbContext, ISqlDatabase<NexusDbContext>, SqlServerRetrievalStore, Task> test)
    {
        var connection = Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未設定 AINEXUS_SQLSERVER_TEST，略過真實 SQL Server 2025 整合測試。");
        var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        var name = "AINexus_Retrieval_Test_" + Guid.NewGuid().ToString("N"); var created = false;
        await using var master = new SqlConnection(settings.ConnectionString); await master.OpenAsync();
        try
        {
            await master.ExecuteAsync($"CREATE DATABASE [{name}]"); created = true; settings.InitialCatalog = name;
            await using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer(settings.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var sql = new DbContextSqlDatabase<NexusDbContext>(db); using var cache = new MemoryCache(new MemoryCacheOptions());
            var store = new SqlServerRetrievalStore(sql, Options.Create(new KnowledgeOptions { VectorCandidates = 1, FtsCandidates = 1, RerankCandidates = 1, TopK = 1 }), cache, NullLogger<SqlServerRetrievalStore>.Instance);
            await test(db, sql, store);
        }
        finally
        {
            if (created) { SqlConnection.ClearAllPools(); await master.ExecuteAsync($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]"); }
        }
    }
}
