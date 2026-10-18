using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Features.Work.Projects;
using AiNexus.Features.Platform.WebSearch;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Chat.Context;

/// <summary>
/// 上下文容量預估請求資料傳輸物件。
/// </summary>
public sealed record ContextPreviewRequest(
    Guid? ConversationId,
    Guid? ParentMessageId,
    string? Prompt,
    string? ModelId,
    IReadOnlyList<Guid>? AttachmentIds = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool WebSearch = false);

/// <summary>
/// 上下文 Token 預估與修剪結果資料傳輸物件。
/// </summary>
public sealed record ContextUsageDto(
    int EstimatedInputTokens,
    int ContextTokens,
    int ReservedOutputTokens,
    int DroppedMessages,
    bool BudgetExceeded,
    bool IsEstimate = true,
    int ReservedKnowledgeTokens = 0,
    int ReservedWebSearchTokens = 0);

/// <summary>
/// 驗證草稿提問字元長度是否低於伺服器全域上限。
/// </summary>
internal sealed class ContextPreviewRequestValidator : RequestValidator<ContextPreviewRequest>
{
    public override string ProblemCode => InferenceErrors.InputTooLongCode;

    public ContextPreviewRequestValidator(IOptions<InferenceOptions> options) =>
        RuleFor(x => x.Prompt)
            .Must(x => x is null || x.Length <= options.Value.MaxInputCharacters)
            .WithErrorCode("length");
}

/// <summary>
/// 草稿提問上下文容量試算端點。
/// 依據提問內容、引用歷史、附件、專案指示以及預留的知識庫檢索與網路搜尋空間，
/// 試算佔用模型的上下文百分比與是否將造成舊訊息修剪。
/// </summary>
internal sealed class PreviewContext(
    ConversationService conversations,
    AttachmentService attachments,
    ModelCatalog models,
    ContextBuilder context,
    IOptions<InferenceOptions> options,
    ModelPolicyService policies,
    KnowledgeRetrieval knowledge,
    ProjectService projects)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/context", (ContextPreviewRequest body, ICurrentUser user, PreviewContext handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(InferenceModule.PromptBodyLimit)
        .WithName("PreviewContext");

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

        // 僅試算上下文容量，不扣減 Token 配額
        if (await policies.RequireAsync(owner, model.Value.Id, ct, checkQuota: false) is { IsSuccess: false } refused)
        {
            return refused.Error;
        }

        // 知識庫與網路搜尋的預留空間以等長空白佔位加入系統提示詞，讓估算與真正生成一致
        var reserved = 0;
        if (body.ConversationId is Guid cid)
        {
            var knowledgeTokens = await knowledge.ReservedContextAsync(owner, cid, ct);
            if (!knowledgeTokens.IsSuccess) return knowledgeTokens.Error;
            reserved = knowledgeTokens.Value;
        }

        var webReserved = body.WebSearch ? WebSearchService.ReservedTokens : 0;

        var systemPromptWithReservations = ContextBuilder.SystemPrompt(options.Value.SystemPrompt, instruction)
            + project
            + new string(' ', reserved + webReserved);

        var parameters = new GenerationParameters(
            model.Value.ContextTokens,
            model.Value.MaxOutputTokens,
            .6,
            systemPromptWithReservations,
            SupportsImages: model.Value.SupportsImages);

        var usage = await context.PreviewAsync(
            body.ConversationId,
            body.ParentMessageId,
            body.Prompt,
            parameters,
            ct,
            files.Value);

        return usage.IsSuccess
            ? usage.Value with { ReservedKnowledgeTokens = reserved, ReservedWebSearchTokens = webReserved }
            : usage;
    }
}
