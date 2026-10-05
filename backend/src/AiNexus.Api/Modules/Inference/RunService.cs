using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Operations;
using AiNexus.Modules.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public sealed class RunService(NexusDbContext db, ConversationService conversations, AttachmentService attachments, AttachmentWriteLock attachmentWrites, GenerationScheduler scheduler, ModelCatalog models, ContextBuilder context, IOptions<InferenceOptions> options, ModelPresentation presentation, AiNexus.Modules.Administration.ModelPolicyService policies, ModelQuotaLock quotaWrites, AiNexus.Modules.Knowledge.KnowledgeRetrieval knowledge, AiNexus.Modules.Projects.ProjectService projects, AiNexus.Modules.WebSearch.WebSearchService webSearch, BillingService billing)
{
    public async Task<GenerationRun> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct)
                  ?? throw new ApiException(404, "run_not_found", "找不到這次生成。");
        await conversations.OwnedAsync(owner, run.ConversationId, ct);
        return run;
    }

    public async Task<RunDto> CreateAsync(Guid owner, CreateRunRequest request, string key, CancellationToken ct)
    {
        if (key.Length is < 16 or > 80 || key.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not '-' and not '_')) throw new ApiException(400, "idempotency_key_required", "請提供 16 至 80 字元的 Idempotency-Key。");
        if (request.Prompt?.Length > options.Value.MaxInputCharacters || request.ModelId?.Length > 160) throw new ApiException(400, "input_too_long", "提問或模型識別碼過長。");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        var previous = await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);
        if (previous is not null)
        {
            await conversations.OwnedAsync(owner, previous.ConversationId, ct);
            if (previous.RequestHash != hash) throw new ApiException(409, "idempotency_conflict", "此提交識別碼已用於不同內容。");
            return presentation.Run(previous);
        }
        // Provider discovery can take seconds. Never hold the cancellation/state gate over it.
        var profile = await models.RequireAsync(request.ModelId, ct);
        var effort = ModelCatalog.RequireReasoning(profile, request.ReasoningEffort);
        var knowledgeSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
        var sources = await knowledge.ForRunAsync(owner, request, ct, knowledgeSelection.CollectionIds);
        AiNexus.Modules.WebSearch.WebSearchRecord? search = null;
        if (request.WebSearch)
        {
            await conversations.OwnedAsync(owner, request.ConversationId, ct);
            await policies.RequireAsync(owner, profile.Id, ct);
            if (!scheduler.Ready) throw new ApiException(503, "scheduler_unavailable", "生成服務尚未就緒。");
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) throw new ApiException(409, "generation_active", "請先等待目前的回答完成。");
            var query = request.RegenerateUserMessageId is Guid old ? await db.Messages.Where(x => x.Id == old && x.ConversationId == request.ConversationId && x.Role == "user").Select(x => x.Content).SingleOrDefaultAsync(ct) : request.Prompt;
            search = await webSearch.SearchAsync(owner, request.ConversationId, key, hash, query ?? "", ct);
        }
        await scheduler.StateGate.WaitAsync(ct);
        var reserved = false;
        var attachmentsLocked = false;
        var quotaLocked = false;
        try
        {
            var existing = await db.Runs.SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);
            if (existing is not null)
            {
                await conversations.OwnedAsync(owner, existing.ConversationId, ct);
                if (existing.RequestHash != hash) throw new ApiException(409, "idempotency_conflict", "此提交識別碼已用於不同內容。");
                return presentation.Run(existing);
            }
            if (!scheduler.Ready) throw new ApiException(503, "scheduler_unavailable", "生成服務尚未就緒，請稍後重試。");
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) throw new ApiException(409, "generation_active", "你已有一則排隊或生成中的訊息，請先等待或停止。");
            await quotaWrites.Gate.WaitAsync(ct); quotaLocked = true;
            if (!(reserved = scheduler.TryReserve())) throw new ApiException(429, "queue_full", "生成佇列已滿，請稍後再試。");
            // Keep unbound file removal and binding in the same short critical section.
            await attachmentWrites.Gate.WaitAsync(ct);
            attachmentsLocked = true;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            await policies.RequireAsync(owner, profile.Id, ct);
            var conversation = await conversations.OwnedAsync(owner, request.ConversationId, ct);
            var projectContext = await projects.ContextAsync(owner, conversation.ProjectId, ct);
            var currentSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
            if (!currentSelection.CollectionIds.Order().SequenceEqual(knowledgeSelection.CollectionIds.Order())) throw new ApiException(409, "knowledge_selection_changed", "知識來源剛剛已變更，請重新送出提問。");
            await knowledge.ValidateHitsAsync(owner, sources, ct);
            if (request.RegenerateUserMessageId is not null && request.AttachmentIds?.Count > 0) throw new ApiException(400, "invalid_request", "重新生成會使用原始提問的附件。");
            var files = await attachments.RequireAsync(owner, request.AttachmentIds, ct);
            var run = new GenerationRun
            {
                OwnerId = owner, ActiveOwnerId = owner, ConversationId = request.ConversationId,
                ExecutorId = scheduler.InstanceId, LeaseExpiresAt = DateTimeOffset.UtcNow + GenerationScheduler.LeaseDuration,
                ModelId = profile.Id, IdempotencyKey = key, RequestHash = hash,
                ParametersJson = JsonSerializer.Serialize(new GenerationParameters(profile.ContextTokens, profile.MaxOutputTokens, 0.6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, conversation.SystemInstruction) + projectContext + AiNexus.Modules.Knowledge.KnowledgeRetrieval.Prompt(sources) + AiNexus.Modules.WebSearch.WebSearchService.Prompt(search), effort, profile.ReasoningControl, profile.SupportsImages))
            };
            await billing.ReserveAsync(run.Id, owner, request.ConversationId, options.Value.Provider, profile.Id, "chat", run.CreatedAt, ct);
            var (user, assistant) = await conversations.PrepareGenerationAsync(owner, request with { ModelId = profile.Id }, run.Id, ct);
            run.UserMessageId = user.Id;
            run.AssistantMessageId = assistant.Id;
            if (search is not null) search.RunId = run.Id;
            knowledge.Bind(assistant.Id, sources);
            foreach (var file in files) db.Set<MessageAttachment>().Add(new() { MessageId = user.Id, AttachmentId = file.Id });
            var fileIds = files.Select(x => x.Id).ToArray();
            await db.Set<Attachment>().Where(x => fileIds.Contains(x.Id) && x.OwnerId == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            await context.BuildAsync(run.ConversationId, user.Id, JsonSerializer.Deserialize<GenerationParameters>(run.ParametersJson)!, ct);
            db.Runs.Add(run);
            AddEvent(db, run, "status");
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, Action = "run.created", ResourceId = run.Id, Result = run.Status });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            scheduler.Enqueue(run.Id);
            reserved = false;
            return presentation.Run(run);
        }
        finally
        {
            if (reserved) scheduler.ReleaseReservation();
            if (attachmentsLocked) attachmentWrites.Gate.Release();
            if (quotaLocked) quotaWrites.Gate.Release();
            scheduler.StateGate.Release();
        }
    }

    public async Task<RunDto> CancelAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            var run = await OwnedAsync(owner, id, ct);
            if (!RunStates.IsActive(run.Status)) return presentation.Run(run);
            scheduler.Cancel(id);
            await FinishAsync(run, RunStates.Cancelled, null, ct);
            return presentation.Run(run);
        }
        finally { scheduler.StateGate.Release(); }
    }

    // Called under StateGate, so cancellation and token flushes cannot overwrite each other.
    public async Task FinishAsync(GenerationRun run, string status, string? error, CancellationToken ct)
    {
        run.Status = status;
        run.ErrorCode = error;
        run.ActiveOwnerId = null;
        run.LeaseExpiresAt = null;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await billing.FinishAsync(run.Id, status, ct);
        AddEvent(db, run, "status");
        await conversations.UpdateAnswerAsync(run, ct);
        db.AuditEvents.Add(new AuditEvent { OwnerId = run.OwnerId, Action = "run.finished", ResourceId = run.Id, Result = error ?? status });
        await db.SaveChangesAsync(ct);
    }

    public static void AddEvent(NexusDbContext db, GenerationRun run, string type, string? delta = null)
    {
        run.LastSequence++;
        db.RunEvents.Add(new RunEvent { RunId = run.Id, Sequence = run.LastSequence, Type = type, Status = run.Status, Delta = delta, ErrorCode = run.ErrorCode });
    }
}
