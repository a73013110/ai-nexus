using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Administration;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

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
internal sealed class CreateRun(NexusDbContext db, ConversationService conversations, AttachmentService attachments, AttachmentWriteLock attachmentWrites, GenerationScheduler scheduler, ModelCatalog models, ContextBuilder context, IOptions<InferenceOptions> options, ModelPresentation presentation, ModelPolicyService policies, ModelQuotaLock quotaWrites, KnowledgeRetrieval knowledge, ProjectService projects, WebSearchService webSearch, BillingService billing, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/runs", async (CreateRunRequest body, HttpContext http, ICurrentUser user, CreateRun handler, CancellationToken ct) =>
        {
            var run = await handler.HandleAsync(user.Id, body, http.Request.Headers["Idempotency-Key"].ToString(), ct);
            return run.IsSuccess ? Results.Accepted($"/api/v1/runs/{run.Value.Id}", run.Value) : run.Error.ToProblem();
        })
        .WithRequestBodyLimit(InferenceModule.PromptBodyLimit).WithName("CreateRun").Produces<RunDto>(202);

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
        // Provider discovery can take seconds. Never hold the conversation's generation lock over it.
        var profile = await models.RequireAsync(request.ModelId, ct);
        var effort = ModelCatalog.RequireReasoning(profile, request.ReasoningEffort);
        var knowledgeSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
        var sources = await knowledge.ForRunAsync(owner, request, ct, knowledgeSelection.CollectionIds);
        WebSearchRecord? search = null;
        if (request.WebSearch)
        {
            await conversations.OwnedAsync(owner, request.ConversationId, ct);
            await policies.RequireAsync(owner, profile.Id, ct);
            if (!scheduler.Ready) throw new ApiException(503, "scheduler_unavailable", "生成服務尚未就緒。");
            if (await db.Runs.AnyAsync(x => x.ActiveOwnerId == owner, ct)) return InferenceErrors.GenerationActive;
            var query = request.RegenerateUserMessageId is Guid old ? await db.Messages.Where(x => x.Id == old && x.ConversationId == request.ConversationId && x.Role == "user").Select(x => x.Content).SingleOrDefaultAsync(ct) : request.Prompt;
            search = await webSearch.SearchAsync(owner, request.ConversationId, key, hash, query ?? "", ct);
        }
        using var conversationLock = await scheduler.LockConversationAsync(request.ConversationId, ct);
        var reserved = false;
        var attachmentsLocked = false;
        var quotaLocked = false;
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
            await quotaWrites.Gate.WaitAsync(ct); quotaLocked = true;
            if (!(reserved = scheduler.TryReserve())) return InferenceErrors.QueueFull;
            // Keep unbound file removal and binding in the same short critical section.
            await attachmentWrites.Gate.WaitAsync(ct);
            attachmentsLocked = true;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
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
            await policies.RequireAsync(owner, profile.Id, ct);
            var conversation = await conversations.OwnedAsync(owner, request.ConversationId, ct);
            var projectContext = await projects.ContextAsync(owner, conversation.ProjectId, ct);
            var currentSelection = await knowledge.SelectionAsync(owner, request.ConversationId, ct);
            if (!currentSelection.CollectionIds.Order().SequenceEqual(knowledgeSelection.CollectionIds.Order())) return InferenceErrors.KnowledgeSelectionChanged;
            await knowledge.ValidateHitsAsync(owner, sources, ct);
            if (request.RegenerateUserMessageId is not null && request.AttachmentIds?.Count > 0) return InferenceErrors.RegenerateWithAttachments;
            var files = await attachments.RequireAsync(owner, request.AttachmentIds, ct);
            var now = clock.GetUtcNow();
            var run = new GenerationRun
            {
                OwnerId = owner, ActiveOwnerId = owner, ConversationId = request.ConversationId, CreatedAt = now,
                ExecutorId = scheduler.InstanceId, LeaseExpiresAt = now + GenerationScheduler.LeaseDuration,
                ModelId = profile.Id, Provider = profile.Provider, ProviderModelId = profile.NativeId, IdempotencyKey = key, RequestHash = hash,
                ParametersJson = JsonSerializer.Serialize(new GenerationParameters(profile.ContextTokens, profile.MaxOutputTokens, 0.6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, conversation.SystemInstruction) + projectContext + KnowledgeRetrieval.Prompt(sources) + WebSearchService.Prompt(search), effort, profile.ReasoningControl, profile.SupportsImages))
            };
            run.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString(); run.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString(); run.OperationId = run.Id;
            await billing.ReserveAsync(run.Id, owner, request.ConversationId, profile.Provider, profile.NativeId, "chat", run.CreatedAt, ct);
            var (user, assistant) = await conversations.PrepareGenerationAsync(owner, request with { ModelId = profile.Id }, run.Id, ct);
            run.UserMessageId = user.Id;
            run.AssistantMessageId = assistant.Id;
            if (search is not null) search.RunId = run.Id;
            knowledge.Bind(assistant.Id, sources);
            foreach (var file in files) db.Set<MessageAttachment>().Add(new() { MessageId = user.Id, AttachmentId = file.Id });
            var fileIds = files.Select(x => x.Id).ToArray();
            await db.Set<Attachment>().Where(x => fileIds.Contains(x.Id) && x.OwnerId == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            var parameters = JsonSerializer.Deserialize<GenerationParameters>(run.ParametersJson)!;
            var messages = await context.BuildAsync(run.ConversationId, user.Id, parameters, ct);
            var inputEstimate = ContextBuilder.Estimate(messages);
            parameters = await policies.BudgetAsync(owner, profile.Id, parameters, inputEstimate, run.CreatedAt, ct);
            run.ReservedTokens = inputEstimate + parameters.MaxOutputTokens;
            run.ParametersJson = JsonSerializer.Serialize(parameters);
            db.Runs.Add(run);
            RunService.AddEvent(db, run, "status");
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, Action = "run.created", ResourceId = run.Id, Result = run.Status });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            scheduler.Enqueue(run, profile.Provider);
            reserved = false;
            return presentation.Run(run);
        }
        finally
        {
            if (reserved) scheduler.ReleaseReservation();
            if (attachmentsLocked) attachmentWrites.Gate.Release();
            if (quotaLocked) quotaWrites.Gate.Release();
        }
    }
}
