using AiNexus.Platform.Http;
using AiNexus.Features.Identity;
using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Conversations;

public static class ConversationEndpoints
{
    public static void MapConversations(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/conversations").RequireAuthorization(Policies.Chat).WithTags("Conversations");
        routes.MapGet("", async (string? search, int? offset, string? view, string? label, CurrentUser current, ConversationService service, CancellationToken ct) => Results.Ok(await service.ListAsync((await current.GetAsync(ct)).Id, search, offset ?? 0, ct, view ?? "active", label))).WithName("ListConversations").Produces<IReadOnlyList<ConversationDto>>();
        routes.MapGet("/labels", async (CurrentUser current, ConversationOrganization organization, CancellationToken ct) => Results.Ok(await organization.LabelsAsync((await current.GetAsync(ct)).Id, ct))).WithName("ListConversationLabels").Produces<IReadOnlyList<string>>();
        routes.MapPost("/import", async (ConversationBackup body, CurrentUser current, ConversationOrganization organization, CancellationToken ct) => Results.Ok(await organization.ImportAsync((await current.GetAsync(ct)).Id, body, ct))).WithRequestBodyLimit(ConversationsModule.ImportBodyLimit).WithName("ImportConversation").Produces<ConversationDto>();
        routes.MapPost("", async (CreateConversationRequest body, CurrentUser current, ConversationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync((await current.GetAsync(ct)).Id, body.Title, ct);
            return Results.Created($"/api/v1/conversations/{created.Id}", created);
        }).WithName("CreateConversation").Produces<ConversationDto>(201);
        routes.MapGet("/{id:guid}", async (Guid id, CurrentUser current, ConversationService service, CancellationToken ct) => Results.Ok(await service.DetailAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("GetConversation").Produces<ConversationDetailDto>();
        routes.MapPatch("/{id:guid}", async (Guid id, RenameConversationRequest body, CurrentUser current, ConversationService service, CancellationToken ct) => Results.Ok(await service.RenameAsync((await current.GetAsync(ct)).Id, id, body.Title, ct))).WithName("RenameConversation").Produces<ConversationDto>();
        routes.MapPatch("/{id:guid}/settings", async (Guid id, ConversationSettingsRequest body, CurrentUser current, ConversationOrganization organization, CancellationToken ct) => Results.Ok(await organization.UpdateAsync((await current.GetAsync(ct)).Id, id, body, ct))).WithName("UpdateConversationSettings").Produces<ConversationDto>();
        routes.MapPost("/{id:guid}/duplicate", async (Guid id, CurrentUser current, ConversationOrganization organization, CancellationToken ct) => Results.Ok(await organization.DuplicateAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("DuplicateConversation").Produces<ConversationDto>();
        routes.MapGet("/{id:guid}/export", async (Guid id, CurrentUser current, ConversationOrganization organization, CancellationToken ct) => Results.Ok(await organization.ExportAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("ExportConversation").Produces<ConversationBackup>();
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
