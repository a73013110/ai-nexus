using AiNexus.Features.Inference;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Account;

internal sealed class GetUserSettings(ModelPresentation models)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings", (ICurrentUser user, GetUserSettings handler) => TypedResults.Ok(handler.Handle(user.User)))
        .WithName("GetUserSettings");

    public UserSettingsDto Handle(NexusUser user) => user.Preferences.ToSettingsDto(models);
}
