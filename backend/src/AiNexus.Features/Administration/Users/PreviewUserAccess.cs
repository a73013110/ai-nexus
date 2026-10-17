using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Users;

/// <summary>使用者目前實際取得的角色、群組與功能，並附上這些功能所在的選單群組（唯讀預覽）。</summary>
internal sealed class PreviewUserAccess(NexusDbContext db, AccessService access)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/access", async (Guid id, PreviewUserAccess handler, CancellationToken ct) => (await handler.HandleAsync(id, ct)).ToHttpResult())
        .WithName("PreviewUserAccess");

    public async Task<Result<AccessDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id, ct)) return AdministrationErrors.NotFound;
        return await access.WithNavigationAsync(id, ct);
    }
}
