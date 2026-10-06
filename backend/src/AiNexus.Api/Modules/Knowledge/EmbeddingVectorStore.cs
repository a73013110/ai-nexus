using System.Text.Json;
using AiNexus.BuildingBlocks;
using Dapper;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Modules.Knowledge;

public sealed class EmbeddingRow
{
    public Guid ChunkId { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public SqlVector<float> Vector { get; set; }
}
public sealed record EmbeddingWrite(Guid ChunkId, byte[] ContentHash, float[] Vector);
public sealed class EmbeddingVectorStore(NexusDbContext db)
{
    public IQueryable<EmbeddingRow> Rows(EmbeddingProfile profile) => profile.Dimensions switch
    {
        768 => db.Set<ChunkEmbedding768>().AsNoTracking().Where(x => x.ProfileId == profile.Id).Select(x => new EmbeddingRow { ChunkId = x.ChunkId, ContentHash = x.ContentHash, Vector = x.Vector }),
        1024 => db.Set<ChunkEmbedding1024>().AsNoTracking().Where(x => x.ProfileId == profile.Id).Select(x => new EmbeddingRow { ChunkId = x.ChunkId, ContentHash = x.ContentHash, Vector = x.Vector }),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
    public async Task<IReadOnlyList<Guid>> ExistingAsync(EmbeddingProfile profile, Guid document, CancellationToken ct) =>
        await Rows(profile).Where(e => db.Set<KnowledgeChunk>().Any(c => c.Id == e.ChunkId && c.DocumentId == document)).Select(e => e.ChunkId).ToListAsync(ct);
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
        if (db.Database.IsSqlServer())
        {
            var parameters = new DynamicParameters(); parameters.Add("Profile", profile.Id);
            var rows = new List<string>();
            for (var i = 0; i < values.Count; i++)
            {
                parameters.Add("Chunk" + i, values[i].ChunkId); parameters.Add("Hash" + i, values[i].ContentHash);
                parameters.Add("Vector" + i, JsonSerializer.Serialize(values[i].Vector));
                rows.Add($"(@Chunk{i}, @Hash{i}, CAST(@Vector{i} AS VECTOR({profile.Dimensions})))");
            }
            var table = VectorDimensions.Table(profile.Dimensions);
            var command = $"INSERT INTO {table} ([ChunkId], [ProfileId], [ContentHash], [Vector]) SELECT v.[ChunkId], @Profile, v.[Hash], v.[Vector] FROM (VALUES {string.Join(',', rows)}) v([ChunkId], [Hash], [Vector]) WHERE NOT EXISTS (SELECT 1 FROM {table} e WITH (UPDLOCK, HOLDLOCK) WHERE e.[ProfileId] = @Profile AND e.[ChunkId] = v.[ChunkId])";
            await db.Database.GetDbConnection().ExecuteAsync(new CommandDefinition(command, parameters, db.Database.CurrentTransaction?.GetDbTransaction(), commandTimeout: 30, cancellationToken: ct));
        }
        else
        {
            var ids = values.Select(x => x.ChunkId).ToArray(); var existing = await Rows(profile).Where(x => ids.Contains(x.ChunkId)).Select(x => x.ChunkId).ToListAsync(ct);
            foreach (var value in values.Where(x => !existing.Contains(x.ChunkId)))
                if (profile.Dimensions == 768) db.Add(new ChunkEmbedding768 { ChunkId = value.ChunkId, ProfileId = profile.Id, ContentHash = value.ContentHash, Vector = new(value.Vector) });
                else db.Add(new ChunkEmbedding1024 { ChunkId = value.ChunkId, ProfileId = profile.Id, ContentHash = value.ContentHash, Vector = new(value.Vector) });
            await db.SaveChangesAsync(ct);
        }
    }
}
