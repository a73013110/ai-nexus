using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Account;

internal sealed class GetUserSettings(ModelPresentation models) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/settings", (ICurrentUser user, GetUserSettings handler) => handler.Handle(user.User))
        .NotAudited("使用者自己的設定")
        .WithName("GetUserSettings");

    public UserSettingsDto Handle(User user) => user.Preferences.ToSettingsDto(models);
}
