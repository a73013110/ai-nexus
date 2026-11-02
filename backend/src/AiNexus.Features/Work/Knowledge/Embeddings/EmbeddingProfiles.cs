using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Admin.AccessControl;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed class EmbeddingProfiles(AppDbContext db, IOptions<KnowledgeOptions> options, AdministrativeWriteLock writes, TimeProvider clock)
{
    public async Task<EmbeddingProfile> TargetAsync(CancellationToken ct)
    {
        var settings = options.Value; var key = EmbeddingInput.Profile(settings);
        // 每次索引與管理頁都會走到這裡；模型沒換時只查一列、不取鎖。
        var current = await db.EmbeddingProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        if (current is not null && current.Status != "retired") return current;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // 「沒有 active 才直接啟用」跨多列，與啟用模型共用檢索管理的鎖；唯一索引（Key、active）是最後防線。
        await writes.AcquireAsync(FeatureCatalog.KnowledgeRetrieval, ct);
        var target = await db.EmbeddingProfiles.SingleOrDefaultAsync(x => x.Key == key, ct);
        if (target is null)
        {
            var active = await db.EmbeddingProfiles.AnyAsync(x => x.Status == "active", ct);
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
    public async Task<EmbeddingProfile> ActiveAsync(CancellationToken ct)
    {
        var active = await db.EmbeddingProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Status == "active", ct);
        return active ?? await TargetAsync(ct);
    }
}
