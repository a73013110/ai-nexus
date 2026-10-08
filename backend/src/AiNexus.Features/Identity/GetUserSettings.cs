using AiNexus.Features.Inference;

namespace AiNexus.Features.Identity;

internal static class GetUserSettings
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings", (ICurrentUser user, ModelPresentation models) => Results.Ok(user.User.Preferences.ToSettingsDto(models)))
        .WithName("GetUserSettings").Produces<UserSettingsDto>();
}
