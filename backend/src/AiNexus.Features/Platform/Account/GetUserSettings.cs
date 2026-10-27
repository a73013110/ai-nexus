using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.Account;

internal sealed class GetUserSettings(ModelPresentation models)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings", (ICurrentUser user, GetUserSettings handler) => TypedResults.Ok(handler.Handle(user.User)))
        .NotAudited("使用者自己的設定")
        .WithName("GetUserSettings");

    public UserSettingsDto Handle(User user) => user.Preferences.ToSettingsDto(models);
}
