using System.Data;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class EmbeddingProfiles(NexusDbContext db, IOptions<KnowledgeOptions> options, KnowledgeWriteLock writes)
{
    public async Task<EmbeddingProfile> TargetAsync(CancellationToken ct)
    {
        var settings = options.Value; var key = EmbeddingInput.Profile(settings);
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var target = await db.Set<EmbeddingProfile>().SingleOrDefaultAsync(x => x.Key == key, ct);
            if (target is null)
            {
                var active = await db.Set<EmbeddingProfile>().AnyAsync(x => x.Status == "active", ct);
                target = new() { Key = key, Provider = settings.EmbeddingProvider, Model = settings.EmbeddingModel, Dimensions = settings.Dimensions,
                    InputFormat = settings.InputFormat, QueryInstruction = settings.QueryInstruction, Revision = settings.Revision,
                    ChunkerConfiguration = JsonSerializer.Serialize(ChunkerSnapshot.Capture(settings)), Status = active ? "building" : "active",
                    ActivatedAt = active ? null : DateTimeOffset.UtcNow };
                db.Add(target); await db.SaveChangesAsync(ct);
            }
            else if (target.Status == "retired")
            {
                target.Status = "building"; target.RetiredAt = null; await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct); return target;
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<EmbeddingProfile> ActiveAsync(CancellationToken ct)
    {
        var active = await db.Set<EmbeddingProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Status == "active", ct);
        return active ?? await TargetAsync(ct);
    }
    public static ChunkerSnapshot ChunkerSettings(EmbeddingProfile profile)
    {
        var snapshot = JsonSerializer.Deserialize<ChunkerSnapshot>(profile.ChunkerConfiguration)
            ?? throw new ApiException(409, "chunker_configuration_invalid", "索引切段設定快照無效，請重新建立 profile。");
        if (snapshot.Version != StructuredChunker.Version) throw new ApiException(409, "chunker_version_unsupported", "此切段器版本已不受支援，請先重建並啟用新 profile。");
        return snapshot;
    }
}
