using AiNexus.Features.Administration;
using AiNexus.Features.Attachments;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Projects;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public sealed record ContextPreviewRequest(Guid? ConversationId, Guid? ParentMessageId, string? Prompt, string? ModelId, IReadOnlyList<Guid>? AttachmentIds = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool WebSearch = false);
public sealed record ContextUsageDto(int EstimatedInputTokens, int ContextTokens, int ReservedOutputTokens, int DroppedMessages, bool BudgetExceeded, bool IsEstimate = true, int ReservedKnowledgeTokens = 0, int ReservedWebSearchTokens = 0);

/// <summary>The draft prompt may not exceed the configured input limit.</summary>
internal sealed class ContextPreviewRequestValidator : RequestValidator<ContextPreviewRequest>
{
    public override string ProblemCode => InferenceErrors.InputTooLongCode;

    public ContextPreviewRequestValidator(IOptions<InferenceOptions> options)
        => RuleFor(x => x.Prompt).Must(x => x is null || x.Length <= options.Value.MaxInputCharacters).WithErrorCode("length");
}

/// <summary>Estimates how much of the model's context a draft prompt, its history, files and reserved retrieval space would use.</summary>
internal sealed class PreviewContext(ConversationService conversations, AttachmentService attachments, ModelCatalog models, ContextBuilder context, IOptions<InferenceOptions> options, ModelPolicyService policies, KnowledgeRetrieval knowledge, ProjectService projects)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/context", async (ContextPreviewRequest body, ICurrentUser user, PreviewContext handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(user.Id, body, ct)))
        .WithRequestBodyLimit(InferenceModule.PromptBodyLimit).WithName("PreviewContext").Produces<ContextUsageDto>();

    public async Task<ContextUsageDto> HandleAsync(Guid owner, ContextPreviewRequest body, CancellationToken ct)
    {
        var instruction = body.ConversationId is Guid id ? (await conversations.OwnedAsync(owner, id, ct)).SystemInstruction : "";
        var project = body.ConversationId is Guid projectConversation ? await projects.ContextAsync(owner, (await conversations.OwnedAsync(owner, projectConversation, ct)).ProjectId, ct) : "";
        var files = await attachments.RequireAsync(owner, body.AttachmentIds, ct);
        var model = await models.RequireAsync(body.ModelId, ct);
        await policies.RequireAsync(owner, model.Id, ct, checkQuota: false);
        var reserved = body.ConversationId is Guid cid ? await knowledge.ReservedContextAsync(owner, cid, ct) : 0;
        var webReserved = body.WebSearch ? WebSearchService.ReservedTokens : 0;
        return (await context.PreviewAsync(body.ConversationId, body.ParentMessageId, body.Prompt, new(model.ContextTokens, model.MaxOutputTokens, .6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, instruction) + project + new string(' ', reserved + webReserved), SupportsImages: model.SupportsImages), ct, files)) with { ReservedKnowledgeTokens = reserved, ReservedWebSearchTokens = webReserved };
    }
}
