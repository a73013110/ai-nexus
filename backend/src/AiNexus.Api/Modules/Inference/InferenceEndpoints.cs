using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Attachments;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public static class InferenceEndpoints
{
    public static void MapInference(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("").RequireAuthorization(BuiltInAccess.ChatPolicy).WithTags("Inference");
        api.MapGet("/models", async (ModelCatalog models, CancellationToken ct) => Results.Ok(await models.GetAsync(ct))).WithName("ListModels").Produces<ModelsDto>();
        api.MapPost("/context", async (ContextPreviewRequest body, CurrentUser current, ConversationService conversations, AttachmentService attachments, ModelCatalog models, ContextBuilder context, IOptions<InferenceOptions> options, CancellationToken ct) =>
        {
            if (body.Prompt?.Length > options.Value.MaxInputCharacters) throw new ApiException(400, "input_too_long", "訊息超過字數上限。");
            var owner = (await current.GetAsync(ct)).Id;
            var instruction = body.ConversationId is Guid id ? (await conversations.OwnedAsync(owner, id, ct)).SystemInstruction : "";
            var files = await attachments.RequireAsync(owner, body.AttachmentIds, ct);
            var model = await models.RequireAsync(body.ModelId, ct);
            return Results.Ok(await context.PreviewAsync(body.ConversationId, body.ParentMessageId, body.Prompt, new(model.ContextTokens, model.MaxOutputTokens, .6, ContextBuilder.SystemPrompt(options.Value.SystemPrompt, instruction), SupportsImages: model.SupportsImages), ct, files));
        }).WithName("PreviewContext").Produces<ContextUsageDto>();
        api.MapPost("/runs", async (CreateRunRequest body, HttpContext http, CurrentUser current, RunService service, CancellationToken ct) =>
        {
            var run = await service.CreateAsync((await current.GetAsync(ct)).Id, body, http.Request.Headers["Idempotency-Key"].ToString(), ct);
            return Results.Accepted($"/api/v1/runs/{run.Id}", run);
        }).WithName("CreateRun").Produces<RunDto>(202);
        api.MapGet("/runs/{id:guid}", async (Guid id, CurrentUser current, RunService service, ModelPresentation models, CancellationToken ct) => Results.Ok(models.Run(await service.OwnedAsync((await current.GetAsync(ct)).Id, id, ct)))).WithName("GetRun").Produces<RunDto>();
        api.MapPost("/runs/{id:guid}/cancel", async (Guid id, CurrentUser current, RunService service, CancellationToken ct) => Results.Ok(await service.CancelAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("CancelRun").Produces<RunDto>();
        api.MapGet("/runs/{id:guid}/events", async (Guid id, long? after, HttpContext http, CurrentUser current, RunService service, NexusDbContext db, SubscriptionLimits limits, CancellationToken ct) =>
            await RunEventsEndpoint.StreamAsync(http, (await current.GetAsync(ct)).Id, id, after, service, db, limits, ct)).WithName("RunEvents").Produces<RunEventDto>(200, "text/event-stream");
    }
}
