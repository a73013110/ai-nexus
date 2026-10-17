using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Account;

public sealed record MeDto(Guid Id, string Account, string DisplayName, PreferencesDto Preferences, string CsrfToken, Guid? ActiveRunId, AccessDto Access);

/// <summary>登入者資料、外觀偏好、新的 CSRF token、進行中的生成任務與存取權限。</summary>
internal sealed class GetMe(NexusDbContext db, IAntiforgery csrf, AccessService access, ModelPresentation models)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/me", async (HttpContext http, ICurrentUser user, GetMe handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(http, user.User, ct)))
        .WithName("GetMe");

    public async Task<MeDto> HandleAsync(HttpContext http, NexusUser user, CancellationToken ct)
    {
        var active = await db.Runs.AsNoTracking().Where(x => x.ActiveOwnerId == user.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        WebSecurity.NoStore(http.Response);
        return new MeDto(user.Id, user.Account, user.DisplayName, models.Preferences(user.Preferences), csrf.GetAndStoreTokens(http).RequestToken!, active, await access.WithNavigationAsync(user.Id, ct));
    }
}
