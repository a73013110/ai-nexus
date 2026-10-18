using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed class EmbeddingProfiles(AppDbContext db, IOptions<KnowledgeOptions> options, KnowledgeWriteLock writes, TimeProvider clock)
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
                target = new() { Key = key, Provider = settings.Embedding.Provider, Model = settings.Embedding.Model, Dimensions = settings.Embedding.Dimensions,
                    InputFormat = settings.Embedding.InputFormat, QueryInstruction = settings.Embedding.QueryInstruction, Revision = settings.Embedding.Revision,
                    Status = active ? "building" : "active",
                    ActivatedAt = active ? null : clock.GetUtcNow() };
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
}
