using AiNexus.Data.App.Identity;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Models;

namespace AiNexus.Features.Account;

internal sealed class GetUserSettings(ModelPresentation models)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings", (ICurrentUser user, GetUserSettings handler) => TypedResults.Ok(handler.Handle(user.User)))
        .WithName("GetUserSettings");

    public UserSettingsDto Handle(User user) => user.Preferences.ToSettingsDto(models);
}
