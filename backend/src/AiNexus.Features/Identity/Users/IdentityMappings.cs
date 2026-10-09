namespace AiNexus.Features.Identity.Users;

public static class IdentityMappings
{
    public static PreferencesDto ToDto(this UserPreferences x) => new(x.Theme, x.ReducedMotion, x.DefaultModelId);
}
