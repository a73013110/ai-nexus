using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using System.Text.Json;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed class EmbeddingRow
{
    public Guid ChunkId { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public SqlVector<float> Vector { get; set; }
}

public sealed class EmbeddingVectorStore(AppDbContext db, ISqlDatabase<AppDbContext> sql)
{
    public IQueryable<EmbeddingRow> Rows(EmbeddingProfile profile) => profile.Dimensions switch
    {
        768 => db.Set<ChunkEmbedding768>().AsNoTracking().Where(x => x.ProfileId == profile.Id).Select(x => new EmbeddingRow { ChunkId = x.ChunkId, ContentHash = x.ContentHash, Vector = x.Vector }),
        1024 => db.Set<ChunkEmbedding1024>().AsNoTracking().Where(x => x.ProfileId == profile.Id).Select(x => new EmbeddingRow { ChunkId = x.ChunkId, ContentHash = x.ContentHash, Vector = x.Vector }),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
    public async Task<IReadOnlyList<Guid>> ExistingAsync(EmbeddingProfile profile, Guid document, CancellationToken ct) =>
        await Rows(profile).Where(e => db.Set<Chunk>().Any(c => c.Id == e.ChunkId && c.DocumentId == document)).Select(e => e.ChunkId).ToListAsync(ct);
    public async Task<Dictionary<string, float[]>> CachedAsync(EmbeddingProfile profile, IReadOnlyList<byte[]> hashes, CancellationToken ct)
    {
        if (hashes.Count == 0) return new(StringComparer.Ordinal);
        var cache = new Dictionary<string, float[]>(StringComparer.Ordinal);
        foreach (var batch in hashes.DistinctBy(Convert.ToHexString).Chunk(256))
        {
            var values = await Rows(profile).Where(x => batch.Contains(x.ContentHash)).GroupBy(x => x.ContentHash).Select(x => x.First()).ToListAsync(ct);
            foreach (var value in values) cache.TryAdd(Convert.ToHexString(value.ContentHash), value.Vector.Memory.ToArray());
        }
        return cache;
    }
    public async Task WriteBatchAsync(EmbeddingProfile profile, IReadOnlyList<EmbeddingWrite> values, CancellationToken ct)
    {
        if (values.Count == 0) return;
        // 整批一個 JSON 參數（鍵名 camelCase 對應 procedure 的 OPENJSON 路徑）：批次文字固定，不受 2100 個參數上限限制。
        var rows = JsonSerializer.Serialize(values.Select(x => new { x.ChunkId, ContentHash = Convert.ToHexString(x.ContentHash), x.Vector }), JsonSerializerOptions.Web);
        await sql.QueryProcedureAsync<int>("knowledge.usp_WriteChunkEmbeddings", new { ProfileId = profile.Id, profile.Dimensions, Rows = rows }, commandTimeout: 30, cancellationToken: ct);
    }
}
