using AiNexus.Platform.Http;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public static class InferenceEndpoints
{
    public static void MapInference(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("").RequireAuthorization(Policies.Chat).WithTags("Inference");
        api.MapGet("/models", async (ModelCatalog models, CurrentUser current, AiNexus.Features.Administration.ModelPolicyService policies, CancellationToken ct) =>
        {
            var catalog = await models.GetAsync(ct);
            var policy = await policies.ForAsync((await current.GetAsync(ct)).Id, ct);
            var allowed = catalog.Models.Where(x => policy.AllowedModelIds is null || policy.AllowedModelIds.Contains(x.Id)).ToArray();
            var defaultId = allowed.Any(x => x.Id == catalog.Policy.DefaultModelId) ? catalog.Policy.DefaultModelId : allowed.FirstOrDefault()?.Id;
            return Results.Ok(catalog with { Models = allowed, Notice = catalog.Models.Count > 0 && allowed.Length == 0 ? "你的群組目前沒有可用模型，請由管理員確認群組允許的模型與目前服務設定。" : catalog.Notice,
                Policy = catalog.Policy with { DefaultModelId = defaultId } });
        }).WithName("ListModels").Produces<ModelsDto>();
        api.MapPost("/context", async (ContextPreviewRequest body, CurrentUser current, ConversationService conversations, AttachmentService attachments, ModelCatalog models, ContextBuilder context, IOptions<InferenceOptions> options, AiNexus.Features.Administration.ModelPolicyService policies, AiNexus.Features.Knowledge.KnowledgeRetrieval knowledge, AiNexus.Features.Projects.ProjectService projects, CancellationToken ct) =>
        {
            if (body.Prompt?.Length > options.Value.MaxInputCharacters) throw new ApiException(400, "input_too_long", "訊息超過字數上限。");
            var owner = (await current.GetAsync(ct)).Id;
            var instruction = body.ConversationId is Guid id ? (await conversations.OwnedAsync(owner, id, ct)).SystemInstruction : "";
            var project = body.ConversationId is Guid projectConversation ? await projects.ContextAsync(owner, (await conversations.OwnedAsync(owner, projectConversation, ct)).ProjectId, ct) : "";
            var files = await attachments.RequireAsync(owner, body.AttachmentIds, ct);
            var model = await models.RequireAsync(body.ModelId, ct);
            await policies.RequireAsync(owner, model.Id, ct, checkQuota: false);
            var reserved = body.ConversationId is Guid cid ? await knowledge.ReservedContextAsync(owner, cid, ct) : 0;
            var webReserved = body.WebSearch ? AiNexus.Features.WebSearch.WebSearchService.ReservedTokens : 0;
            return Results.Ok((await context.PreviewAsync(body.ConversationId, body.ParentMessageId, body.Prompt, new(model.ContextTokens, model.MaxOutputTokens, .6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, instruction) + project + new string(' ', reserved + webReserved), SupportsImages: model.SupportsImages), ct, files)) with { ReservedKnowledgeTokens = reserved, ReservedWebSearchTokens = webReserved });
        }).WithRequestBodyLimit(InferenceModule.PromptBodyLimit).WithName("PreviewContext").Produces<ContextUsageDto>();
        api.MapPost("/runs", async (CreateRunRequest body, HttpContext http, CurrentUser current, RunService service, CancellationToken ct) =>
        {
            var run = await service.CreateAsync((await current.GetAsync(ct)).Id, body, http.Request.Headers["Idempotency-Key"].ToString(), ct);
            return Results.Accepted($"/api/v1/runs/{run.Id}", run);
        }).WithRequestBodyLimit(InferenceModule.PromptBodyLimit).WithName("CreateRun").Produces<RunDto>(202);
        api.MapGet("/runs/{id:guid}", async (Guid id, CurrentUser current, RunService service, ModelPresentation models, CancellationToken ct) => Results.Ok(models.Run(await service.OwnedAsync((await current.GetAsync(ct)).Id, id, ct)))).WithName("GetRun").Produces<RunDto>();
        api.MapPost("/runs/{id:guid}/cancel", async (Guid id, CurrentUser current, RunService service, CancellationToken ct) => Results.Ok(await service.CancelAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("CancelRun").Produces<RunDto>();
        api.MapGet("/runs/{id:guid}/events", async (Guid id, long? after, HttpContext http, CurrentUser current, RunService service, NexusDbContext db, SubscriptionLimits limits, CancellationToken ct) =>
            await RunEventsEndpoint.StreamAsync(http, (await current.GetAsync(ct)).Id, id, after, service, db, limits, ct)).WithName("RunEvents").Produces<RunEventDto>(200, "text/event-stream");
    }
}
