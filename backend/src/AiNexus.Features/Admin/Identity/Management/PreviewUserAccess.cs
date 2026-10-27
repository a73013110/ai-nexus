using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

/// <summary>使用者目前實際取得的角色、群組與功能，並附上與 <c>/me</c> 相同計算的選單樹與資料範圍（唯讀預覽）。</summary>
internal sealed class PreviewUserAccess(AppDbContext db, AccessView access)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/access", async (Guid id, PreviewUserAccess handler, CancellationToken ct) => (await handler.HandleAsync(id, ct)).ToHttpResult())
        .Audited(IdentityAudit.UserAccessRead)
        .WithName("PreviewUserAccess");

    public async Task<Result<AccessDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id, ct)) return IdentityErrors.AdminResourceNotFound;
        return await access.ForUserAsync(id, ct);
    }
}
