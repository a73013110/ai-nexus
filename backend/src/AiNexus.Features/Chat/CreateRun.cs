using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Chat;

public sealed record CreateRunRequest(Guid ConversationId, string? ModelId, string? Prompt, Guid? ParentMessageId, Guid? RegenerateUserMessageId, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ReasoningEffort = null, IReadOnlyList<Guid>? AttachmentIds = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool WebSearch = false);

/// <summary>The prompt may not exceed the configured input limit and a model identifier is at most 160 characters.</summary>
internal sealed class CreateRunRequestValidator : RequestValidator<CreateRunRequest>
{
    public override string ProblemCode => InferenceErrors.InputTooLongCode;

    public CreateRunRequestValidator(IOptions<InferenceOptions> options)
    {
        RuleFor(x => x.Prompt).Must(x => x is null || x.Length <= options.Value.MaxInputCharacters).WithErrorCode("length");
        RuleFor(x => x.ModelId).Must(x => x is null || x.Length <= 160).WithErrorCode("length");
    }
}

/// <summary>
/// Queues an answer for a new or regenerated prompt. The Idempotency-Key makes retries return the same run; one owner
/// has at most one active run. Ownership, model policy, budget and attachment binding are rechecked under the
/// conversation's generation lock, the owner row lock and the attachment write lock, inside one transaction.
/// </summary>
internal sealed class CreateRun(NexusDbContext db, ConversationService conversations, AttachmentService attachments, AttachmentWriteLock attachmentWrites, GenerationScheduler scheduler, ModelCatalog models, ContextBuilder context, IOptions<InferenceOptions> options, ModelPresentation presentation, ModelPolicyService policies, KnowledgeRetrieval knowledge, ProjectService projects, WebSearchService webSearch, BillingService billing, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/runs", async (CreateRunRequest body, HttpContext http, ICurrentUser user, CreateRun handler, CancellationToken ct) =>
        {
            var run = await handler.HandleAsync(user.Id, body, http.Request.Headers["Idempotency-Key"].ToString(), ct);
            return run.IsSuccess ? Results.Accepted($"/api/v1/runs/{run.Value.Id}", run.Value) : run.Error.ToProblem();
        })
        .WithRequestBodyLimit(InferenceModule.PromptBodyLimit).RequireRateLimiting(ChatModule.SendRateLimit).WithName("CreateRun").Produces<RunDto>(202);

    public async Task<Result<RunDto>> HandleAsync(Guid owner, CreateRunRequest request, string key, CancellationToken ct)
    {
        if (key.Length is < 16 or > 80 || key.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not '-' and not '_')) return InferenceErrors.IdempotencyKeyRequired;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        var previous = await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);
        if (previous is not null)
        {
            await conversations.OwnedAsync(owner, previous.ConversationId, ct);
            if (previous.RequestHash != hash) return InferenceErrors.IdempotencyConflict;
            return presentation.Run(previous);
        }
        // Everything that needs no mutual exclusion runs before the locks: provider discovery, policy, retrieval, search
        // and the context. The locked section re-checks what can change, then only reserves and saves.
        var profile = (await models.RequireAsync(request.ModelId, ct)).OrThrow();
        var effort = ModelCatalog.RequireReasoning(profile, request.ReasoningEffort).OrThrow();
        // Fails fast; BudgetAsync enforces model approval and quota again under the owner row lock.
        (await policies.RequireAsync(owner, profile.Id, ct)).OrThrow();
        var knowledgeSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
        var turn = new ConversationTurn(request.ConversationId, request.Prompt, request.ParentMessageId, request.RegenerateUserMessageId);
        var sources = await knowledge.ForRunAsync(owner, turn, ct, knowledgeSelection.CollectionIds);
        WebSearchRecord? search = null;
        if (request.WebSearch)
        {
            await conversations.OwnedAsync(owner, request.ConversationId, ct);
            if (!scheduler.Ready) throw new ApiException(503, "scheduler_unavailable", "生成服務尚未就緒。");
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;
            var query = request.RegenerateUserMessageId is Guid old ? await db.Messages.Where(x => x.Id == old && x.ConversationId == request.ConversationId && x.Role == "user").Select(x => x.Content).SingleOrDefaultAsync(ct) : request.Prompt;
            search = await webSearch.SearchAsync(owner, request.ConversationId, key, hash, query ?? "", ct);
        }
        else
        {
            // Cheap early outs before the context is built; both are checked again under the locks.
            if (!scheduler.Ready) throw new ApiException(503, "scheduler_unavailable", "生成服務尚未就緒，請稍後重試。");
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;
        }
        var prepared = await PrepareAsync(owner, request, profile, sources, search, ct);
        using var conversationLock = await scheduler.LockConversationAsync(request.ConversationId, ct);
        var reserved = false;
        var attachmentsLocked = false;
        try
        {
            var existing = await db.Runs.SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);
            if (existing is not null)
            {
                await conversations.OwnedAsync(owner, existing.ConversationId, ct);
                if (existing.RequestHash != hash) return InferenceErrors.IdempotencyConflict;
                return presentation.Run(existing);
            }
            if (!scheduler.Ready) throw new ApiException(503, "scheduler_unavailable", "生成服務尚未就緒，請稍後重試。");
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;
            if (!(reserved = scheduler.TryReserve())) return InferenceErrors.QueueFull;
            // Keep unbound file removal and binding in the same short critical section; without files nothing is bound.
            if (request.AttachmentIds?.Count > 0)
            {
                await attachmentWrites.Gate.WaitAsync(ct);
                attachmentsLocked = true;
            }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // A harmless update serializes this owner's quota reservations (model and attachment) across requests and hosts.
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            // The conversation lock does not cover the owner's other conversations: under the owner row lock, a request
            // that raced in another conversation has committed, so its run is visible here.
            if (await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct) is { } raced)
            {
                await conversations.OwnedAsync(owner, raced.ConversationId, ct);
                if (raced.RequestHash != hash) return InferenceErrors.IdempotencyConflict;
                return presentation.Run(raced);
            }
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;
            var conversation = await conversations.OwnedAsync(owner, request.ConversationId, ct);
            var projectContext = prepared is not null && prepared.ProjectId == conversation.ProjectId ? prepared.ProjectContext : await projects.ContextAsync(owner, conversation.ProjectId, ct);
            var currentSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
            if (!currentSelection.CollectionIds.Order().SequenceEqual(knowledgeSelection.CollectionIds.Order())) return InferenceErrors.KnowledgeSelectionChanged;
            await knowledge.ValidateHitsAsync(owner, sources, ct);
            if (request.RegenerateUserMessageId is not null && request.AttachmentIds?.Count > 0) return InferenceErrors.RegenerateWithAttachments;
            var files = await attachments.RequireAsync(owner, request.AttachmentIds, ct);
            var now = clock.GetUtcNow();
            var parameters = new GenerationParameters(profile.ContextTokens, profile.MaxOutputTokens, 0.6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, conversation.SystemInstruction) + projectContext + KnowledgeRetrieval.Prompt(sources) + WebSearchService.Prompt(search), effort, profile.ReasoningControl, profile.SupportsImages);
            var run = new GenerationRun
            {
                OwnerId = owner, ActiveOwnerId = owner, ConversationId = request.ConversationId, CreatedAt = now,
                ExecutorId = scheduler.InstanceId, LeaseExpiresAt = now + GenerationScheduler.LeaseDuration,
                ModelId = profile.Id, Provider = profile.Provider, ProviderModelId = profile.NativeId, IdempotencyKey = key, RequestHash = hash,
                ParametersJson = JsonSerializer.Serialize(parameters)
            };
            run.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString(); run.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString(); run.OperationId = run.Id;
            await billing.ReserveAsync(run.Id, owner, request.ConversationId, profile.Provider, profile.NativeId, "chat", run.CreatedAt, ct);
            var (user, assistant) = await conversations.PrepareGenerationAsync(owner, turn, profile.Id, run.Id, ct);
            run.UserMessageId = user.Id;
            run.AssistantMessageId = assistant.Id;
            if (search is not null) search.RunId = run.Id;
            knowledge.Bind(assistant.Id, sources);
            foreach (var file in files) db.Set<MessageAttachment>().Add(new() { MessageId = user.Id, AttachmentId = file.Id });
            var fileIds = files.Select(x => x.Id).ToArray();
            if (fileIds.Length > 0) await db.Set<Attachment>().Where(x => fileIds.Contains(x.Id) && x.OwnerId == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            var messages = prepared?.ContextFor(parameters.SystemPrompt, files)
                ?? await context.PrepareAsync(request.ConversationId, request.ParentMessageId, request.RegenerateUserMessageId, request.Prompt, files, parameters, ct);
            await context.RequireImagesAsync(messages, ct);
            var inputEstimate = MessageCost.Estimate(messages);
            parameters = (await policies.BudgetAsync(owner, profile.Id, parameters, inputEstimate, run.CreatedAt, ct)).OrThrow();
            run.ReservedTokens = inputEstimate + parameters.MaxOutputTokens;
            run.ParametersJson = JsonSerializer.Serialize(parameters);
            db.Runs.Add(run);
            RunService.AddEvent(db, run, "status");
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, Action = "run.created", ResourceId = run.Id, Result = run.Status });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            // BudgetAsync keeps the history budget, so the worker sends exactly this context instead of rebuilding it.
            scheduler.Enqueue(run, profile.Provider, messages);
            reserved = false;
            return presentation.Run(run);
        }
        finally
        {
            if (reserved) scheduler.ReleaseReservation();
            if (attachmentsLocked) attachmentWrites.Gate.Release();
        }
    }

    /// <summary>
    /// The project context and the model context, read before the locks. Answered history does not change, so the
    /// locked section reuses them while the conversation's project, the system prompt and the files are the same, and
    /// rebuilds them otherwise. A failure here is left to the locked section, which reports it in its usual order.
    /// </summary>
    private async Task<PreparedContext?> PrepareAsync(Guid owner, CreateRunRequest request, ModelProfile profile, IReadOnlyList<KnowledgeHitDto> sources, WebSearchRecord? search, CancellationToken ct)
    {
        try
        {
            // Not tracked: the locked section must load the conversation fresh to re-check it.
            var conversation = await db.Conversations.AsNoTracking().Where(x => x.Id == request.ConversationId && x.OwnerId == owner)
                .Select(x => new { x.ProjectId, x.SystemInstruction }).SingleOrDefaultAsync(ct);
            if (conversation is null) return null;
            var projectContext = await projects.ContextAsync(owner, conversation.ProjectId, ct);
            var files = await attachments.RequireAsync(owner, request.AttachmentIds, ct);
            var systemPrompt = ContextBuilder.SystemPrompt(options.Value.SystemPrompt, conversation.SystemInstruction) + projectContext + KnowledgeRetrieval.Prompt(sources) + WebSearchService.Prompt(search);
            var parameters = new GenerationParameters(profile.ContextTokens, profile.MaxOutputTokens, 0.6, systemPrompt, SupportsImages: profile.SupportsImages);
            try { return new(conversation.ProjectId, projectContext, systemPrompt, files, await context.PrepareAsync(request.ConversationId, request.ParentMessageId, request.RegenerateUserMessageId, request.Prompt, files, parameters, ct), null); }
            catch (ApiException error) { return new(conversation.ProjectId, projectContext, systemPrompt, files, null, ExceptionDispatchInfo.Capture(error)); }
        }
        catch (ApiException) { return null; }
    }

    private sealed record PreparedContext(Guid? ProjectId, string ProjectContext, string SystemPrompt, IReadOnlyList<Attachment> Files, IReadOnlyList<InferenceMessage>? Messages, ExceptionDispatchInfo? Failure)
    {
        /// <summary>The prepared context (or its failure, rethrown) when it was built from the same inputs; otherwise null.</summary>
        public IReadOnlyList<InferenceMessage>? ContextFor(string systemPrompt, IReadOnlyList<Attachment> files)
        {
            static (Guid, string, string, string?) Key(Attachment x) => (x.Id, x.FileName, x.ContentType, x.ExtractedText);
            if (systemPrompt != SystemPrompt || !files.Select(Key).SequenceEqual(Files.Select(Key))) return null;
            Failure?.Throw();
            return Messages;
        }
    }
}
