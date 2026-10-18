using AiNexus.Data.App.Identity;

namespace AiNexus.Features.Admin.Identity.Users;

public static class IdentityMappings
{
    public static PreferencesDto ToDto(this UserPreferences x) => new(x.Theme, x.ReducedMotion, x.DefaultModelId);
}
