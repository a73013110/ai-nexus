using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

/// <summary>The roles, groups and features a user currently receives.</summary>
internal sealed class PreviewUserAccess(NexusDbContext db, AccessService access)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/access", async (Guid id, PreviewUserAccess handler, CancellationToken ct) => (await handler.HandleAsync(id, ct)).ToHttpResult())
        .WithName("PreviewUserAccess").Produces<AccessDto>();

    public async Task<Result<AccessDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id, ct)) return AdministrationErrors.NotFound;
        return await access.ForUserAsync(id, ct);
    }
}
