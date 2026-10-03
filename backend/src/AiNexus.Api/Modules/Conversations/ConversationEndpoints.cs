using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;

namespace AiNexus.Modules.Conversations;

public static class ConversationEndpoints
{
    public static void MapConversations(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/conversations").RequireAuthorization(BuiltInAccess.ChatPolicy).WithTags("Conversations");
        routes.MapGet("", async (string? search, int? offset, CurrentUser current, ConversationService service, CancellationToken ct) => Results.Ok(await service.ListAsync((await current.GetAsync(ct)).Id, search, offset ?? 0, ct))).WithName("ListConversations").Produces<IReadOnlyList<ConversationDto>>();
        routes.MapPost("", async (CreateConversationRequest body, CurrentUser current, ConversationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync((await current.GetAsync(ct)).Id, body.Title, ct);
            return Results.Created($"/api/v1/conversations/{created.Id}", created);
        }).WithName("CreateConversation").Produces<ConversationDto>(201);
        routes.MapGet("/{id:guid}", async (Guid id, CurrentUser current, ConversationService service, CancellationToken ct) => Results.Ok(await service.DetailAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("GetConversation").Produces<ConversationDetailDto>();
        routes.MapPatch("/{id:guid}", async (Guid id, RenameConversationRequest body, CurrentUser current, ConversationService service, CancellationToken ct) => Results.Ok(await service.RenameAsync((await current.GetAsync(ct)).Id, id, body.Title, ct))).WithName("RenameConversation").Produces<ConversationDto>();
        routes.MapPatch("/{id:guid}/branch", async (Guid id, SelectBranchRequest body, CurrentUser current, ConversationService service, CancellationToken ct) =>
        {
            await service.SelectBranchAsync((await current.GetAsync(ct)).Id, id, body.LeafId, ct);
            return Results.NoContent();
        }).WithName("SelectBranch").Produces(204);
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser current, ConversationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync((await current.GetAsync(ct)).Id, id, ct);
            return Results.NoContent();
        }).WithName("DeleteConversation").Produces(204);
    }
}
