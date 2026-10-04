using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Quality;

public sealed class QualityService(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, JobService jobs, ModelCatalog models, ModelPresentation presentation, AiNexus.Modules.Administration.ModelPolicyService policy)
{
    public async Task<FeedbackDto?> FeedbackAsync(Guid actor, Guid message, FeedbackRequest request, CancellationToken ct)
    {
        if (request.Rating is < -1 or > 1 || request.Note.Length > 2000 || request.Reason is not ("" or "incorrect" or "citation" or "incomplete" or "format" or "other")) throw new ApiException(400, "feedback_invalid", "回饋格式不正確，補充說明最多 2,000 字元。");
        var source = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == message && m.Role == "assistant" && c.OwnerId == actor && !c.IsDeleted select new { Message = m, Conversation = c }).SingleOrDefaultAsync(ct) ?? throw Missing();
        if (RunStates.IsActive(source.Message.Status)) throw new ApiException(409, "feedback_answer_pending", "請等回答結束再提供回饋。");
        await writes.Gate.WaitAsync(ct);
        try
        {
            var row = await db.Set<MessageFeedback>().FindAsync([message], ct);
            if (request.Rating == 0) { if (row is not null) db.Remove(row); await db.SaveChangesAsync(ct); return null; }
            if (row is null) { row = new() { MessageId = message, OwnerId = actor }; db.Add(row); }
            row.Rating = request.Rating; row.Reason = request.Reason; row.Note = request.Note.Trim(); row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct); return new(message, source.Conversation.Id, source.Conversation.Title, row.Rating, row.Reason, row.Note, row.UpdatedAt);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<IReadOnlyList<FeedbackDto>> FeedbackListAsync(Guid actor, CancellationToken ct) => await
        (from f in db.Set<MessageFeedback>() join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
         where f.OwnerId == actor && !c.IsDeleted orderby f.UpdatedAt descending
         select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).Take(100).ToListAsync(ct);
    public async Task<FeedbackDto?> FeedbackForAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == id && m.Role == "assistant" && c.OwnerId == actor && !c.IsDeleted select m.Id).AnyAsync(ct)) throw Missing();
        return await (from f in db.Set<MessageFeedback>() join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id where f.MessageId == id && f.OwnerId == actor select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).SingleOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<EvaluationSetDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var resources = await (await access.QueryAsync(actor, "evaluation", ct)).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct);
        var ids = resources.Select(x => x.Id).ToArray(); var sets = await db.Set<EvaluationSet>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var result = new List<EvaluationSetDto>(); foreach (var resource in resources) result.Add(Describe(sets[resource.Id], await access.DescribeAsync(actor, resource, ct))); return result;
    }
    public async Task<EvaluationSetDto> GetAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, "evaluation", ct);
        return Describe(await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == id, ct), await access.DescribeAsync(actor, resource, ct));
    }
    public async Task<EvaluationSetDto> SaveAsync(Guid actor, Guid? id, EvaluationSetRequest request, CancellationToken ct)
    {
        Validate(request); await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct); WorkspaceResource resource;
            if (id is Guid existing)
            {
                resource = await access.RequireAsync(actor, existing, "evaluation", ct, write: true);
                var changed = await db.Set<EvaluationSet>().Where(x => x.Id == existing && x.Version == request.ExpectedVersion).ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.Description, request.Description.Trim()).SetProperty(x => x.CasesJson, JsonSerializer.Serialize(request.Cases, (JsonSerializerOptions?)null)), ct);
                if (changed != 1) throw new ApiException(409, "evaluation_set_conflict", "題庫已被其他人修改，請重新載入後合併。目前輸入仍保留。");
                resource.Name = ResourceAccess.Name(request.Name); resource.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                if (await db.Set<WorkspaceResource>().CountAsync(x => x.Kind == "evaluation" && x.OwnerId == actor && !x.IsDeleted, ct) >= 100) throw new ApiException(409, "evaluation_set_limit", "個人評測集已達 100 個。");
                resource = new() { OwnerId = actor, Kind = "evaluation", Name = ResourceAccess.Name(request.Name) }; db.Add(resource);
                db.Add(new EvaluationSet { Id = resource.Id, Description = request.Description.Trim(), CasesJson = JsonSerializer.Serialize(request.Cases) });
            }
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "quality.set.saved", ResourceId = resource.Id, Result = "saved" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await GetAsync(actor, resource.Id, ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<EvaluationRunDto> RunAsync(Guid actor, Guid setId, EvaluationRunRequest request, CancellationToken ct)
    {
        if (request.Variants.Count is < 1 or > 3 || request.Variants.Any(x => x.Label.Trim().Length is < 1 or > 80 || x.Instruction.Length > 4000) || request.Variants.Select(x => x.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Variants.Count) throw new ApiException(400, "evaluation_variants_invalid", "請設定 1 至 3 個名稱不同的比較方案，指令最多 4,000 字元。");
        var variants = new List<EvaluationVariant>();
        foreach (var variant in request.Variants) { var model = await models.RequireAsync(variant.ModelId, ct); await policy.RequireAsync(actor, model.Id, ct); variants.Add(new(variant.Label.Trim(), model.Id, variant.Instruction.Trim())); }
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var resource = await access.RequireAsync(actor, setId, "evaluation", ct);
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.OwnerId == actor && x.Kind == "evaluation" && x.ActiveKey != null, ct)) throw new ApiException(409, "evaluation_active", "你已有處理中的評測，請先完成或取消。");
            if (await db.Set<EvaluationRun>().CountAsync(x => x.OwnerId == actor, ct) >= 500) throw new ApiException(409, "evaluation_run_limit", "個人評測已達 500 次保留上限。");
            var set = await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == setId, ct);
            var run = new EvaluationRun { SetId = setId, OwnerId = actor, SetTitle = resource.Name, SetVersion = set.Version, CasesJson = set.CasesJson, VariantsJson = JsonSerializer.Serialize(variants) };
            var job = jobs.Enqueue(actor, setId, run.Id, "evaluation", "品質評測 · " + resource.Name); run.JobId = job.Id; db.Add(run);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = run.Id, Action = "quality.run.queued", Result = "queued" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Describe(run, job, actor);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<IReadOnlyList<EvaluationRunDto>> RunsAsync(Guid actor, Guid setId, CancellationToken ct)
    {
        await access.RequireAsync(actor, setId, "evaluation", ct);
        var rows = await (from r in db.Set<EvaluationRun>() join j in db.Set<BackgroundJob>() on r.JobId equals j.Id where r.SetId == setId orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        return rows.Select(x => Describe(x.r, x.j, actor)).ToList();
    }
    public async Task<EvaluationDetailDto> DetailAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        var resource = await access.RequireAsync(actor, run.SetId, "evaluation", ct); var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == run.JobId, ct);
        var variants = Parse<EvaluationVariant>(run.VariantsJson).Select(x => x with { ModelId = presentation.PublicId(x.ModelId) }).ToArray();
        var results = await db.Set<EvaluationResult>().AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CaseIndex).ThenBy(x => x.VariantIndex).ToListAsync(ct);
        return new(Describe(run, job, actor), Parse<EvaluationCase>(run.CasesJson), variants, results.Select(x => new EvaluationResultDto(x.CaseIndex, x.VariantIndex, x.Output, x.Truncated, x.RequiredMatches, x.RequiredTotal, x.ForbiddenMatches, x.ElapsedMs, x.InputTokens, x.OutputTokens, x.ReviewScore, x.ReviewNote)).ToArray(), await access.CanEditAsync(actor, resource, ct));
    }
    public async Task ReviewAsync(Guid actor, Guid id, int caseIndex, int variantIndex, ReviewRequest request, CancellationToken ct)
    {
        if (request.Score is < 1 or > 5 || request.Note.Length > 2000) throw new ApiException(400, "review_invalid", "人工評分介於 1 至 5，說明最多 2,000 字元。");
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); await access.RequireAsync(actor, run.SetId, "evaluation", ct, write: true);
        var result = await db.Set<EvaluationResult>().SingleOrDefaultAsync(x => x.RunId == id && x.CaseIndex == caseIndex && x.VariantIndex == variantIndex, ct) ?? throw Missing();
        result.ReviewScore = request.Score; result.ReviewNote = request.Note.Trim(); result.ReviewerId = actor;
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "quality.result.reviewed", Result = "saved", DetailsJson = JsonSerializer.Serialize(new { caseIndex, variantIndex, request.Score }) }); await db.SaveChangesAsync(ct);
    }
    public static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json)!;
    private static EvaluationSetDto Describe(EvaluationSet value, ResourceDto resource) => new(resource, value.Description, Parse<EvaluationCase>(value.CasesJson), value.Version);
    private static EvaluationRunDto Describe(EvaluationRun r, BackgroundJob j, Guid actor) => new(r.Id, r.SetId, r.SetTitle, r.SetVersion, r.OwnerId == actor, r.CreatedAt, JobService.Describe(j));
    private static ApiException Missing() => new(404, "quality_item_missing", "找不到此回饋或評測項目。");
    private static void Validate(EvaluationSetRequest request)
    {
        ResourceAccess.Name(request.Name);
        if (request.Description.Length > 2000 || request.Cases.Count is < 1 or > 20 || request.Cases.Any(x => x.Question.Trim().Length is < 1 or > 4000 || x.Reference.Length > 4000 || !TermsValid(x.RequiredTerms) || !TermsValid(x.ForbiddenTerms))) throw new ApiException(400, "evaluation_cases_invalid", "題庫需有 1 至 20 題；問題與參考答案各最多 4,000 字元，關鍵字各最多 20 個、每個 80 字元。");
    }
    private static bool TermsValid(IReadOnlyList<string>? terms) => terms is null || terms.Count <= 20 && terms.All(x => x.Trim().Length is > 0 and <= 80) && terms.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == terms.Count;
}
