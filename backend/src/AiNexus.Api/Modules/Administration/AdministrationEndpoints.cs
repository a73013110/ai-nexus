using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;

namespace AiNexus.Modules.Administration;

public static class AdministrationEndpoints
{
    public static void MapAdministration(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/admin").RequireAuthorization(AdministrationConfiguration.Policy).WithTags("Administration");
        api.MapGet("/catalog", async (AdministrationService service, CancellationToken ct) => Results.Ok(await service.CatalogAsync(ct))).WithName("GetAdminCatalog").Produces<AdminCatalogDto>();
        api.MapGet("/users", async (string? search, int? offset, AdministrationService service, CancellationToken ct) => Results.Ok(await service.UsersAsync(search, offset ?? 0, ct))).WithName("ListAdminUsers").Produces<AdminUsersDto>();
        api.MapPost("/users", async (UserAccountRequest body, UserAccountAdministration service, CancellationToken ct) => Results.Ok(new CreatedUserDto(await service.SaveAsync(null, body, ct)))).WithName("CreateAdminUser").Produces<CreatedUserDto>();
        api.MapPut("/users/{id:guid}", async (Guid id, UserAccountRequest body, UserAccountAdministration service, CancellationToken ct) => { await service.SaveAsync(id, body, ct); return Results.NoContent(); }).WithName("SaveAdminUser");
        api.MapDelete("/users/{id:guid}", async (Guid id, UserAccountAdministration service, CancellationToken ct) => { await service.DeleteAsync(id, ct); return Results.NoContent(); }).WithName("DeleteAdminUser");
        api.MapGet("/users/{id:guid}/insights", async (Guid id, AdministrativeReader service, CancellationToken ct) => Results.Ok(await service.UserAsync(id, ct))).WithName("GetAdminUserInsights").Produces<AdminUserDetailDto>();
        api.MapGet("/users/{id:guid}/conversations", async (Guid id, string? search, int? offset, bool? includeDeleted, AdministrativeReader service, CancellationToken ct) => Results.Ok(await service.ConversationsAsync(id, search, offset ?? 0, includeDeleted ?? false, ct))).WithName("ListAdminUserConversations").Produces<AdminConversationPageDto>();
        api.MapGet("/conversations/{id:guid}", async (Guid id, int? offset, AdministrativeReader service, CancellationToken ct) => Results.Ok(await service.ConversationAsync(id, offset ?? 0, ct))).WithName("ReadAdminConversation").Produces<AdminConversationDetailDto>();
        api.MapPut("/users/{id:guid}/roles", async (Guid id, UserRolesRequest body, AdministrationService service, CancellationToken ct) => { await service.SetUserRolesAsync(id, body, ct); return Results.NoContent(); }).WithName("SetUserRoles");
        api.MapPut("/users/{id:guid}/storage", async (Guid id, AiNexus.Modules.Attachments.AttachmentStorageLimitRequest body, AdministrationService service, CancellationToken ct) => { await service.SetStorageAsync(id, body, ct); return Results.NoContent(); }).WithName("SetUserAttachmentStorage").Produces(204);
        api.MapGet("/users/{id:guid}/access", async (Guid id, AdministrationService service, CancellationToken ct) => Results.Ok(await service.EffectiveAsync(id, ct))).WithName("PreviewUserAccess").Produces<AccessDto>();
        api.MapPut("/roles/{id}", async (string id, RoleUpdateRequest body, AdministrationService service, CancellationToken ct) => { await service.SaveRoleAsync(id, body, ct); return Results.NoContent(); }).WithName("SaveRole");
        api.MapPut("/groups/{id}", async (string id, GroupUpdateRequest body, AdministrationService service, CancellationToken ct) => { await service.SaveGroupAsync(id, body, ct); return Results.NoContent(); }).WithName("SaveRoleGroup");
        api.MapPut("/features/{id}", async (string id, FeatureUpdateRequest body, AdministrationService service, CancellationToken ct) => { await service.SaveFeatureAsync(id, body, ct); return Results.NoContent(); }).WithName("SaveFeature");
        api.MapGet("/audit", async (long? before, string? search, string? action, string? result, DateTimeOffset? from, DateTimeOffset? until, AdministrationService service, CancellationToken ct) => Results.Ok(await service.AuditAsync(before, ct, search, action, result, from, until))).WithName("ListAdminAudit").Produces<IReadOnlyList<AuditDto>>();
        api.MapGet("/usage", async (AdministrationService service, CancellationToken ct) => Results.Ok(await service.UsageAsync(ct))).WithName("GetAdminUsage").Produces<AdminUsageDto>();
        root.MapGet("/settings/model-policy", async (CurrentUser current, ModelPolicyService service, CancellationToken ct) => Results.Ok(await service.ForAsync((await current.GetAsync(ct)).Id, ct))).WithName("GetEffectiveModelPolicy").Produces<EffectiveModelPolicyDto>();
    }
}
