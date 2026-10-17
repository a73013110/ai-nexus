using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Chat.Context;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.ModelPolicies;
using AiNexus.Features.Inference.ModelTasks;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Chat.Runs;

/// <summary>
/// 建立生成任務請求資料傳輸物件。
/// </summary>
public sealed record CreateRunRequest(
    Guid ConversationId,
    string? ModelId,
    string? Prompt,
    Guid? ParentMessageId,
    Guid? RegenerateUserMessageId,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? ReasoningEffort = null,
    IReadOnlyList<Guid>? AttachmentIds = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool WebSearch = false);

/// <summary>
/// 提問請求內容長度驗證器。
/// </summary>
internal sealed class CreateRunRequestValidator : RequestValidator<CreateRunRequest>
{
    public override string ProblemCode => InferenceErrors.InputTooLongCode;

    public CreateRunRequestValidator(IOptions<InferenceOptions> options)
    {
        RuleFor(x => x.Prompt)
            .Must(x => x is null || x.Length <= options.Value.MaxInputCharacters)
            .WithErrorCode("length");

        RuleFor(x => x.ModelId)
            .Must(x => x is null || x.Length <= 160)
            .WithErrorCode("length");
    }
}

/// <summary>
/// 建立生成任務端點，把新提問或重新生成放進伺服器佇列。
/// 冪等鍵加上請求雜湊，讓重試不會重複扣款或重複生成。知識庫檢索、網路搜尋與上下文組裝在鎖外先做（較慢），
/// 進鎖後再重驗，因為鎖外到鎖內之間選取範圍或權限可能已變動。
/// </summary>
internal sealed class CreateRun(
    NexusDbContext db,
    ConversationService conversations,
    AttachmentService attachments,
    AttachmentWriteLock attachmentWrites,
    GenerationScheduler scheduler,
    ModelCatalog models,
    ContextBuilder context,
    IOptions<InferenceOptions> options,
    ModelPresentation presentation,
    ModelPolicyService policies,
    KnowledgeRetrieval knowledge,
    ProjectService projects,
    WebSearchService webSearch,
    BillingService billing,
    TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/runs", async (CreateRunRequest body, HttpContext http, ICurrentUser user, CreateRun handler, CancellationToken ct) =>
        {
            var run = await handler.HandleAsync(user.Id, body, http.Request.Headers["Idempotency-Key"].ToString(), ct);
            return run.ToHttpResult(created => TypedResults.Accepted($"/api/v1/runs/{created.Id}", created));
        })
        .WithRequestBodyLimit(InferenceModule.PromptBodyLimit)
        .RequireRateLimiting(ChatModule.SendRateLimit)
        .WithName("CreateRun");

    public async Task<Result<RunDto>> HandleAsync(Guid owner, CreateRunRequest request, string key, CancellationToken ct)
    {
        if (key.Length is < 16 or > 80 || key.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not '-' and not '_'))
        {
            return InferenceErrors.IdempotencyKeyRequired;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

        // 鎖外的快速路徑：同一 Key 的重送直接回放既有結果；進鎖後還會再比對一次
        var previous = await db.Runs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);

        if (previous is not null)
        {
            if (await conversations.RequireOwnedAsync(owner, previous.ConversationId, ct) is { IsSuccess: false } denied)
            {
                return denied.Error;
            }

            if (previous.RequestHash != hash)
            {
                return InferenceErrors.IdempotencyConflict;
            }

            return presentation.Run(previous);
        }

        // 以下皆在對話鎖之外執行，進鎖後會重驗
        var found = await models.RequireAsync(request.ModelId, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;

        var effort = ModelCatalog.RequireReasoning(profile, request.ReasoningEffort);
        if (!effort.IsSuccess) return effort.Error;

        // 快速權限檢查；嚴格的配額計算在鎖內由 BudgetAsync 完成
        if (await policies.RequireAsync(owner, profile.Id, ct) is { IsSuccess: false } refused)
        {
            return refused.Error;
        }

        var knowledgeSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
        if (!knowledgeSelection.IsSuccess) return knowledgeSelection.Error;

        var turn = new ConversationTurn(request.ConversationId, request.Prompt, request.ParentMessageId, request.RegenerateUserMessageId);
        var retrieved = await knowledge.ForRunAsync(owner, turn, ct, knowledgeSelection.Value.CollectionIds);
        if (!retrieved.IsSuccess) return retrieved.Error;
        var sources = retrieved.Value;

        WebSearchRecord? search = null;
        if (request.WebSearch)
        {
            if (await conversations.RequireOwnedAsync(owner, request.ConversationId, ct) is { IsSuccess: false } missing)
            {
                return missing.Error;
            }

            if (!scheduler.Ready) return ChatErrors.SchedulerUnavailable;
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;

            var query = request.RegenerateUserMessageId is Guid old
                ? await db.Messages.Where(x => x.Id == old && x.ConversationId == request.ConversationId && x.Role == "user").Select(x => x.Content).SingleOrDefaultAsync(ct)
                : request.Prompt;

            var searched = await webSearch.SearchAsync(owner, request.ConversationId, key, hash, query ?? "", ct);
            if (!searched.IsSuccess) return searched.Error;
            search = searched.Value;
        }
        else
        {
            if (!scheduler.Ready) return ChatErrors.SchedulerUnavailable;
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;
        }

        var prepared = await PrepareAsync(owner, request, profile, sources, search, ct);

        // 取得順序固定為：對話鎖 → 佇列名額 → 附件閘門 → 交易與帳號列鎖（finally 反向釋放）
        using var conversationLock = await scheduler.LockConversationAsync(request.ConversationId, ct);
        var reserved = false;
        var attachmentsLocked = false;

        try
        {
            // 進入鎖後再次進行冪等比對（防止並發同 Key 重複進入）
            var existing = await db.Runs.SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);
            if (existing is not null)
            {
                if (await conversations.RequireOwnedAsync(owner, existing.ConversationId, ct) is { IsSuccess: false } denied)
                {
                    return denied.Error;
                }

                if (existing.RequestHash != hash)
                {
                    return InferenceErrors.IdempotencyConflict;
                }

                return presentation.Run(existing);
            }

            if (!scheduler.Ready) return ChatErrors.SchedulerUnavailable;
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;

            if (!(reserved = scheduler.TryReserve()))
            {
                return InferenceErrors.QueueFull;
            }

            // 鎖定附件寫入閘門，防止未綁定的附件在短暫空檔被清理任務刪除
            if (request.AttachmentIds?.Count > 0)
            {
                await attachmentWrites.Gate.WaitAsync(ct);
                attachmentsLocked = true;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // 透過無害更新觸發帳號行鎖，將同使用者跨對話、跨主機的提問請求串行化
            await db.Users
                .Where(x => x.Id == owner)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);

            // 在行鎖下再次確認無競態請求
            if (await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct) is { } raced)
            {
                if (await conversations.RequireOwnedAsync(owner, raced.ConversationId, ct) is { IsSuccess: false } denied)
                {
                    return denied.Error;
                }

                if (raced.RequestHash != hash)
                {
                    return InferenceErrors.IdempotencyConflict;
                }

                return presentation.Run(raced);
            }

            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct))
            {
                return InferenceErrors.GenerationActive;
            }

            var owned = await conversations.OwnedAsync(owner, request.ConversationId, ct);
            if (!owned.IsSuccess) return owned.Error;
            var conversation = owned.Value;

            var projectContext = prepared is not null && prepared.ProjectId == conversation.ProjectId
                ? prepared.ProjectContext
                : await projects.ContextAsync(owner, conversation.ProjectId, ct);

            if (!projectContext.IsSuccess) return projectContext.Error;

            // 鎖外檢索到現在可能已改動，選取範圍與命中來源的權限都要重驗
            var currentSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
            if (!currentSelection.IsSuccess) return currentSelection.Error;
            if (!currentSelection.Value.CollectionIds.Order().SequenceEqual(knowledgeSelection.Value.CollectionIds.Order()))
            {
                return InferenceErrors.KnowledgeSelectionChanged;
            }

            if (await knowledge.ValidateHitsAsync(owner, sources, ct) is { IsSuccess: false } revoked)
            {
                return revoked.Error;
            }

            if (request.RegenerateUserMessageId is not null && request.AttachmentIds?.Count > 0)
            {
                return InferenceErrors.RegenerateWithAttachments;
            }

            var bound = await attachments.RequireAsync(owner, request.AttachmentIds, ct);
            if (!bound.IsSuccess) return bound.Error;
            var files = bound.Value;

            var now = clock.GetUtcNow();
            var systemPrompt = ContextBuilder.SystemPrompt(options.Value.SystemPrompt, conversation.SystemInstruction)
                + projectContext.Value
                + KnowledgeRetrieval.Prompt(sources)
                + WebSearchService.Prompt(search);

            var parameters = new GenerationParameters(
                profile.ContextTokens,
                profile.MaxOutputTokens,
                0.6,
                systemPrompt,
                effort.Value,
                profile.ReasoningControl,
                profile.SupportsImages);

            var run = new GenerationRun
            {
                OwnerId = owner,
                ActiveOwnerId = owner,
                ConversationId = request.ConversationId,
                CreatedAt = now,
                ExecutorId = scheduler.InstanceId,
                LeaseExpiresAt = now + GenerationScheduler.LeaseDuration,
                ModelId = profile.Id,
                Provider = profile.Provider,
                ProviderModelId = profile.NativeId,
                IdempotencyKey = key,
                RequestHash = hash,
                ParametersJson = JsonSerializer.Serialize(parameters)
            };

            run.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
            run.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString();
            run.OperationId = run.Id;

            await billing.ReserveAsync(run.Id, owner, request.ConversationId, profile.Provider, profile.NativeId, "chat", run.CreatedAt, ct);

            var saved = await conversations.PrepareGenerationAsync(conversation, turn, profile.Id, run.Id, ct);
            if (!saved.IsSuccess) return saved.Error;
            var (user, assistant) = saved.Value;

            run.UserMessageId = user.Id;
            run.AssistantMessageId = assistant.Id;

            if (search is not null) search.RunId = run.Id;
            knowledge.Bind(assistant.Id, sources);

            foreach (var file in files)
            {
                db.Set<MessageAttachment>().Add(new() { MessageId = user.Id, AttachmentId = file.Id });
            }

            var fileIds = files.Select(x => x.Id).ToArray();
            if (fileIds.Length > 0)
            {
                await db.Set<Attachment>()
                    .Where(x => fileIds.Contains(x.Id) && x.OwnerId == owner)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            }

            // 嘗試複用鎖前準備好的上下文；若有變更則重新 Prepare
            var built = prepared?.ContextFor(parameters.SystemPrompt, files)
                ?? await context.PrepareAsync(request.ConversationId, request.ParentMessageId, request.RegenerateUserMessageId, request.Prompt, files, parameters, ct);

            if (!built.IsSuccess) return built.Error;
            var messages = built.Value;

            if (await context.RequireImagesAsync(messages, ct) is { IsSuccess: false } unavailable)
            {
                return unavailable.Error;
            }

            var inputEstimate = MessageCost.Estimate(messages);
            var budget = await policies.BudgetAsync(owner, profile.Id, parameters, inputEstimate, run.CreatedAt, ct);
            if (!budget.IsSuccess) return budget.Error;

            parameters = budget.Value;
            run.ReservedTokens = inputEstimate + parameters.MaxOutputTokens;
            run.ParametersJson = JsonSerializer.Serialize(parameters);

            db.Runs.Add(run);
            RunService.AddEvent(db, run, "status");
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, Action = "run.created", ResourceId = run.Id, Result = run.Status });

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            scheduler.Enqueue(run, profile.Provider, messages);
            reserved = false; // 已成功入隊，finally 中不需要釋放

            return presentation.Run(run);
        }
        finally
        {
            if (reserved) scheduler.ReleaseReservation();
            if (attachmentsLocked) attachmentWrites.Gate.Release();
        }
    }

    /// <summary>
    /// 在取得排他鎖前預先進行草稿上下文組裝。
    /// </summary>
    private async Task<PreparedContext?> PrepareAsync(
        Guid owner,
        CreateRunRequest request,
        ModelProfile profile,
        IReadOnlyList<KnowledgeHitDto> sources,
        WebSearchRecord? search,
        CancellationToken ct)
    {
        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(x => x.Id == request.ConversationId && x.OwnerId == owner)
            .Select(x => new { x.ProjectId, x.SystemInstruction })
            .SingleOrDefaultAsync(ct);

        if (conversation is null) return null;

        var projectContext = await projects.ContextAsync(owner, conversation.ProjectId, ct);
        if (!projectContext.IsSuccess) return null;

        var files = await attachments.RequireAsync(owner, request.AttachmentIds, ct);
        if (!files.IsSuccess) return null;

        var systemPrompt = ContextBuilder.SystemPrompt(options.Value.SystemPrompt, conversation.SystemInstruction)
            + projectContext.Value
            + KnowledgeRetrieval.Prompt(sources)
            + WebSearchService.Prompt(search);

        var parameters = new GenerationParameters(
            profile.ContextTokens,
            profile.MaxOutputTokens,
            0.6,
            systemPrompt,
            SupportsImages: profile.SupportsImages);

        return new(
            conversation.ProjectId,
            projectContext.Value,
            systemPrompt,
            files.Value,
            await context.PrepareAsync(request.ConversationId, request.ParentMessageId, request.RegenerateUserMessageId, request.Prompt, files.Value, parameters, ct));
    }

    private sealed record PreparedContext(
        Guid? ProjectId,
        string ProjectContext,
        string SystemPrompt,
        IReadOnlyList<Attachment> Files,
        Result<IReadOnlyList<InferenceMessage>> Messages)
    {
        /// <summary>參數與附件都未變時複用鎖外準備的上下文；任一不同即回傳 null，由呼叫端重新組裝。</summary>
        public Result<IReadOnlyList<InferenceMessage>>? ContextFor(string systemPrompt, IReadOnlyList<Attachment> files)
        {
            static (Guid, string, string, string?) Key(Attachment x) => (x.Id, x.FileName, x.ContentType, x.ExtractedText);
            if (systemPrompt != SystemPrompt || !files.Select(Key).SequenceEqual(Files.Select(Key))) return null;
            return Messages;
        }
    }
}
