using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Knowledge;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Quality;

public sealed class RetrievalEvaluationService(NexusDbContext db, RetrievalAuthorization authorization, AccessService features,
    EmbeddingProfiles profiles, IOptions<KnowledgeOptions> options, IOptions<InferenceOptions> inference, ResourceWriteLock writes, JobService jobs)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<RetrievalEvaluationDto> CreateAsync(Guid actor, RetrievalEvaluationRequest request, CancellationToken ct)
    {
        Validate(request); await RequireAccessAsync(actor, request.CollectionIds, ct);
        var ids = request.Cases.SelectMany(x => x.Relevant).Select(x => x.DocumentId).Distinct().ToArray();
        var documents = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.Status == "ready" && x.CollectionId != null && request.CollectionIds.Contains(x.CollectionId.Value)).Select(x => x.Id).ToListAsync(ct);
        if (documents.Count != ids.Length) throw new ApiException(400, "retrieval_corpus_invalid", "相關文件必須已完成索引且位於選取的知識庫。");
        var pages = await db.Set<DocumentPage>().AsNoTracking().Where(x => ids.Contains(x.DocumentId)).Select(x => new { x.DocumentId, x.PageNumber }).ToListAsync(ct);
        if (request.Cases.SelectMany(x => x.Relevant).Any(x => x.Pages.Any(p => !pages.Any(page => page.DocumentId == x.DocumentId && page.PageNumber == p)))) throw new ApiException(400, "retrieval_pages_invalid", "驗收集包含不存在的文件頁碼。");
        await profiles.ActiveAsync(ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await RequireAccessAsync(actor, request.CollectionIds, ct);
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.OwnerId == actor && x.Kind == "retrieval-eval" && x.ActiveKey != null, ct)) throw new ApiException(409, "retrieval_evaluation_active", "已有處理中的檢索評測，請先完成或取消。");
            if (await db.Set<RetrievalEvaluation>().CountAsync(x => x.OwnerId == actor, ct) >= 500) throw new ApiException(409, "retrieval_evaluation_limit", "個人檢索評測已達 500 次保留上限。");
            var profile = await profiles.ActiveAsync(ct);
            var run = new RetrievalEvaluation { OwnerId = actor, Title = request.Title.Trim(), CollectionsJson = JsonSerializer.Serialize(request.CollectionIds, Json), CasesJson = JsonSerializer.Serialize(request.Cases, Json), TopK = options.Value.TopK, ProfileKey = profile.Key, ConfigurationFingerprint = await FingerprintAsync(profile, request.CollectionIds, ct) };
            var job = jobs.Enqueue(actor, null, run.Id, "retrieval-eval", "檢索評測 · " + run.Title); run.JobId = job.Id; db.Add(run);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = run.Id, Action = "quality.retrieval.queued", Result = "queued" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Describe(run, job);
        }
        finally { writes.Gate.Release(); }
    }
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
    public async Task<IReadOnlyList<RetrievalEvaluationDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (from r in db.Set<RetrievalEvaluation>().AsNoTracking() join j in db.Set<BackgroundJob>().AsNoTracking() on r.JobId equals j.Id where r.OwnerId == actor orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        var result = new List<RetrievalEvaluationDto>();
        foreach (var row in rows)
        {
            try { await RequireAccessAsync(actor, Parse<Guid>(row.r.CollectionsJson), ct); result.Add(Describe(row.r, row.j)); }
            catch (ApiException error) when (error.Status is 403 or 404) { }
        }
        return result;
    }
    public async Task<RetrievalReportDto> ReportAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await RequireAsync(actor, id, ct); var cases = Parse<RetrievalEvaluationCase>(run.CasesJson);
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == run.JobId, ct);
        var rows = await db.Set<RetrievalEvaluationResult>().AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CaseIndex).ThenBy(x => x.Mode).ToListAsync(ct);
        var metrics = rows.Select(x => new RetrievalMetricDto(cases[x.CaseIndex].Id, x.Mode, x.ActualMode, x.Unavailable, x.Recall, x.ReciprocalRank, x.Ndcg, x.Refused, x.RewriteMs, x.EmbedMs, x.SearchMs, x.RerankMs, x.ElapsedMs)).ToArray();
        await RequireAccessAsync(actor, Parse<Guid>(run.CollectionsJson), ct);
        return new(Describe(run, job), RetrievalMetrics.Summarize(metrics), metrics);
    }
    private async Task RequireAccessAsync(Guid actor, IReadOnlyList<Guid> collections, CancellationToken ct)
    {
        if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "quality")) throw new ApiException(403, "evaluation_access_revoked", "品質評測功能權限已停用。");
        await authorization.CollectionsAsync(actor, collections, ct);
    }
    private async Task<string> FingerprintAsync(EmbeddingProfile profile, IReadOnlyList<Guid> collections, CancellationToken ct)
    {
        var documents = await (from d in db.Set<KnowledgeDocument>().AsNoTracking() join r in db.Set<WorkspaceResource>().AsNoTracking() on d.Id equals r.Id
            where !d.IsDeleted && d.CollectionId != null && collections.Contains(d.CollectionId.Value) orderby d.Id select new { d.Id, d.TextVersion, r.UpdatedAt, d.Status }).ToArrayAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { profile.Key, Settings = options.Value, Ollama = inference.Value.BaseUrl, Documents = documents }, Json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
    internal static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json, Json)!;
    private static RetrievalEvaluationDto Describe(RetrievalEvaluation run, BackgroundJob job) => new(run.Id, run.Title, Parse<RetrievalEvaluationCase>(run.CasesJson).Length, run.TopK, run.ProfileKey, run.ConfigurationFingerprint, run.CreatedAt, JobService.Describe(job));
    private static void Validate(RetrievalEvaluationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120 || request.CollectionIds is null || request.CollectionIds.Count is < 1 or > 3 || request.Cases is null || request.Cases.Count is < 1 or > 20)
            throw new ApiException(400, "retrieval_evaluation_invalid", "請提供名稱、1 至 3 個知識庫及 1 至 20 題驗收集。");
        if (request.Cases.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 80 || string.IsNullOrWhiteSpace(x.Query) || x.Query.Length > 2000 || x.Relevant is null
            || x.Relevant.Count > 100 || (x.NoAnswer ? x.Relevant.Count != 0 : x.Relevant.Count == 0)
            || x.Relevant.Any(r => r is null || r.DocumentId == Guid.Empty || r.Grade is < 1 or > 3 || r.Pages is null || r.Pages.Count > 100 || r.Pages.Any(p => p < 1) || r.Pages.Distinct().Count() != r.Pages.Count)
            || x.Relevant.Select(r => r.DocumentId).Distinct().Count() != x.Relevant.Count) || request.Cases.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != request.Cases.Count)
            throw new ApiException(400, "retrieval_corpus_invalid", "每題需有唯一識別與最多 2,000 字元查詢；有答案題需標註相關文件、正整數頁碼及 1 至 3 級相關性；無答案題不標註文件。");
    }
}
