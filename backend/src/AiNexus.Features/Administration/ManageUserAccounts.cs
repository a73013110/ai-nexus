using AiNexus.Features.Identity;

namespace AiNexus.Features.Administration;

/// <summary>Creates, edits and deletes accounts through Identity's <see cref="UserAccountAdministration"/>, which audits each change.</summary>
internal static class ManageUserAccounts
{
    public static void MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/users", async (UserAccountRequest body, UserAccountAdministration service, CancellationToken ct) => Results.Ok(new CreatedUserDto(await service.SaveAsync(null, body, ct))))
        .WithName("CreateAdminUser").Produces<CreatedUserDto>();

    public static void MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}", async (Guid id, UserAccountRequest body, UserAccountAdministration service, CancellationToken ct) => { await service.SaveAsync(id, body, ct); return Results.NoContent(); })
        .WithName("SaveAdminUser");

    public static void MapDelete(RouteGroupBuilder routes) => routes
        .MapDelete("/users/{id:guid}", async (Guid id, UserAccountAdministration service, CancellationToken ct) => { await service.DeleteAsync(id, ct); return Results.NoContent(); })
        .WithName("DeleteAdminUser");
}
