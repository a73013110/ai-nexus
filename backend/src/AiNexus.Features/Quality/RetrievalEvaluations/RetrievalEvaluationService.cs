using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

/// <summary>
/// Access and comparability checks shared by the retrieval evaluation slices and <see cref="RetrievalEvaluationHandler"/>.
/// The checks re-run between job steps, so their failures stay exceptions that fail the job with the same code.
/// </summary>
public sealed class RetrievalEvaluationService(NexusDbContext db, RetrievalAuthorization authorization, AccessService features,
    EmbeddingProfiles profiles, IOptions<KnowledgeOptions> options, IOptions<InferenceOptions> inference)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RetrievalEvaluation> RequireAsync(Guid actor, Guid id, CancellationToken ct, bool unchanged = false)
    {
        var run = await db.Set<RetrievalEvaluation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct) ?? throw new ApiException(404, "retrieval_evaluation_missing", "找不到此檢索評測。");
        var collections = Parse<Guid>(run.CollectionsJson); await RequireAccessAsync(actor, collections, ct);
        if (unchanged)
        {
            var profile = await profiles.ActiveAsync(ct);
            if (run.ConfigurationFingerprint != await FingerprintAsync(profile, collections, ct)) throw new ApiException(409, "retrieval_evaluation_changed", "索引、文件版本或檢索設定已變更，請重新建立評測以取得可比較結果。");
        }
        return run;
    }
    internal async Task RequireAccessAsync(Guid actor, IReadOnlyList<Guid> collections, CancellationToken ct)
    {
        if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "quality")) throw new ApiException(403, "evaluation_access_revoked", "品質評測功能權限已停用。");
        (await authorization.CollectionsAsync(actor, collections, ct)).OrThrow();
    }
    internal async Task<string> FingerprintAsync(EmbeddingProfile profile, IReadOnlyList<Guid> collections, CancellationToken ct)
    {
        var documents = await (from d in db.Set<KnowledgeDocument>().AsNoTracking() join r in db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking() on d.Id equals r.Id
            where !d.IsDeleted && d.CollectionId != null && collections.Contains(d.CollectionId.Value) orderby d.Id select new { d.Id, d.TextVersion, r.UpdatedAt, d.Status }).ToArrayAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { profile.Key, Settings = options.Value, Ollama = inference.Value.BaseUrl, Documents = documents }, Json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
    internal static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json, Json)!;
    internal static RetrievalEvaluationDto Describe(RetrievalEvaluation run, BackgroundJob job) => new(run.Id, run.Title, Parse<RetrievalEvaluationCase>(run.CasesJson).Length, run.TopK, run.ProfileKey, run.ConfigurationFingerprint, run.CreatedAt, JobService.Describe(job));
}
