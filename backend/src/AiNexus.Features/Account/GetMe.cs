using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Account;

public sealed record MeDto(Guid Id, string Account, string DisplayName, PreferencesDto Preferences, string CsrfToken, Guid? ActiveRunId, AccessDto Access);

/// <summary>The signed-in user, their appearance preferences, a fresh CSRF token, their active run and their access.</summary>
internal static class GetMe
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/me", async (HttpContext http, ICurrentUser current, NexusDbContext db, IAntiforgery csrf, AccessService access, ModelPresentation models, CancellationToken ct) =>
        {
            var user = current.User;
            var active = await db.Runs.AsNoTracking().Where(x => x.ActiveOwnerId == user.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            WebSecurity.NoStore(http.Response);
            return Results.Ok(new MeDto(user.Id, user.Account, user.DisplayName, models.Preferences(user.Preferences), csrf.GetAndStoreTokens(http).RequestToken!, active, await access.ForUserAsync(user.Id, ct)));
        })
        .WithName("GetMe").Produces<MeDto>();
}
