using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Quality;
using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Embeddings;
using AiNexus.Features.Work.Knowledge.Retrieval;

namespace AiNexus.Features.Admin.Quality.RetrievalEvaluations;

/// <summary>
/// 檢索評測各切片與 <see cref="RetrievalEvaluationHandler"/> 共用的存取與可比較性檢查。
/// 每個工作步驟之間都會重新檢查；失敗時以其代碼讓工作失敗。
/// </summary>
public sealed class RetrievalEvaluationService(AppDbContext db, RetrievalAuthorization authorization, AccessService features,
    EmbeddingProfiles profiles, IOptions<KnowledgeOptions> options, IOptions<InferenceOptions> inference)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>工作執行與重試用：除了擁有者與知識庫權限，也要求索引與設定指紋與建立時相同。</summary>
    public async Task<Result<RetrievalEvaluation>> RequireUnchangedAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await db.Set<RetrievalEvaluation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
        if (run is null) return QualityErrors.RetrievalMissing;
        var collections = Parse<Guid>(run.CollectionsJson);
        if (await RequireAccessAsync(actor, collections, ct) is { IsSuccess: false } denied) return denied.Error;
        var profile = await profiles.ActiveAsync(ct);
        if (run.ConfigurationFingerprint != await FingerprintAsync(profile, collections, ct)) return QualityErrors.RetrievalChanged;
        return run;
    }
    internal async Task<Result> RequireAccessAsync(Guid actor, IReadOnlyList<Guid> collections, CancellationToken ct)
    {
        if (!await features.HoldsAsync(actor, FeatureCatalog.Quality, ct)) return AccessControlErrors.FeatureForbidden;
        return await authorization.CollectionsAsync(actor, collections, ct);
    }
    internal async Task<string> FingerprintAsync(EmbeddingProfile profile, IReadOnlyList<Guid> collections, CancellationToken ct)
    {
        var documents = await (from d in db.Set<Document>().AsNoTracking() join r in db.Set<Resource>().IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking() on d.Id equals r.Id
            where !d.IsDeleted && d.CollectionId != null && collections.Contains(d.CollectionId.Value) orderby d.Id select new { d.Id, d.TextVersion, r.UpdatedAt, d.Status }).ToArrayAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { profile.Key, Settings = options.Value, Ollama = inference.Value.Providers.Ollama.Endpoint, Documents = documents }, Json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
    internal static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json, Json)!;
    internal static RetrievalEvaluationDto Describe(RetrievalEvaluation run, BackgroundJob job) => new(run.Id, run.Title, Parse<RetrievalEvaluationCase>(run.CasesJson).Length, run.TopK, run.ProfileKey, run.ConfigurationFingerprint, run.CreatedAt, JobService.Describe(job));
}
