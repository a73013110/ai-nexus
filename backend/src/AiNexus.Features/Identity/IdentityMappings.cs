namespace AiNexus.Features.Identity;

public static class IdentityMappings
{
    public static PreferencesDto ToDto(this UserPreferences x) => new(x.Theme, x.ReducedMotion, x.DefaultModelId);
}
