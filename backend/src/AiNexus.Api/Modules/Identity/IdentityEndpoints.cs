using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Inference;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Identity;

public static class IdentityEndpoints
{
    public static void MapIdentity(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (HttpContext http, CurrentUser current, NexusDbContext db, IAntiforgery csrf, AccessService access, ModelPresentation models, CancellationToken ct) =>
        {
            var user = await current.GetAsync(ct);
            var active = await db.Runs.AsNoTracking().Where(x => x.ActiveOwnerId == user.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            WebSecurity.NoStore(http.Response);
            return Results.Ok(new MeDto(user.Id, user.Account, user.DisplayName, models.Preferences(user.Preferences), csrf.GetAndStoreTokens(http).RequestToken!, active, await access.ForUserAsync(user.Id, ct)));
        }).WithName("GetMe").Produces<MeDto>();
        api.MapPut("/preferences", async (PreferencesDto body, CurrentUser current, CancellationToken ct) => Results.Ok(await current.UpdatePreferencesAsync(body, ct))).WithName("UpdatePreferences").Produces<PreferencesDto>();
        api.MapGet("/settings", async (PersonalSettingsService service, CancellationToken ct) => Results.Ok(await service.GetAsync(ct))).WithName("GetUserSettings").Produces<UserSettingsDto>();
        api.MapPut("/settings", async (UserSettingsDto body, PersonalSettingsService service, CancellationToken ct) => Results.Ok(await service.SaveAsync(body, ct))).WithName("SaveUserSettings").Produces<UserSettingsDto>();
        api.MapGet("/settings/usage", async (PersonalSettingsService service, CancellationToken ct) => Results.Ok(await service.UsageAsync(ct))).WithName("GetPersonalUsage").Produces<PersonalUsageDto>();
    }
}
