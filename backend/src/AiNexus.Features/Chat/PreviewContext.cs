using AiNexus.Features.Attachments;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Projects;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Chat;

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
        .MapPost("/context", (ContextPreviewRequest body, ICurrentUser user, PreviewContext handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(InferenceModule.PromptBodyLimit).WithName("PreviewContext");

    public async Task<Result<ContextUsageDto>> HandleAsync(Guid owner, ContextPreviewRequest body, CancellationToken ct)
    {
        var instruction = "";
        var project = "";
        if (body.ConversationId is Guid id)
        {
            var conversation = await conversations.OwnedAsync(owner, id, ct);
            if (!conversation.IsSuccess) return conversation.Error;
            instruction = conversation.Value.SystemInstruction;
            var projectContext = await projects.ContextAsync(owner, conversation.Value.ProjectId, ct);
            if (!projectContext.IsSuccess) return projectContext.Error;
            project = projectContext.Value;
        }
        var files = await attachments.RequireAsync(owner, body.AttachmentIds, ct);
        if (!files.IsSuccess) return files.Error;
        var model = await models.RequireAsync(body.ModelId, ct);
        if (!model.IsSuccess) return model.Error;
        if (await policies.RequireAsync(owner, model.Value.Id, ct, checkQuota: false) is { IsSuccess: false } refused) return refused.Error;
        var reserved = 0;
        if (body.ConversationId is Guid cid)
        {
            var knowledgeTokens = await knowledge.ReservedContextAsync(owner, cid, ct);
            if (!knowledgeTokens.IsSuccess) return knowledgeTokens.Error;
            reserved = knowledgeTokens.Value;
        }
        var webReserved = body.WebSearch ? WebSearchService.ReservedTokens : 0;
        var usage = await context.PreviewAsync(body.ConversationId, body.ParentMessageId, body.Prompt, new(model.Value.ContextTokens, model.Value.MaxOutputTokens, .6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, instruction) + project + new string(' ', reserved + webReserved), SupportsImages: model.Value.SupportsImages), ct, files.Value);
        return usage.IsSuccess ? usage.Value with { ReservedKnowledgeTokens = reserved, ReservedWebSearchTokens = webReserved } : usage;
    }
}
