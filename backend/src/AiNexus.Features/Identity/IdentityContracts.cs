namespace AiNexus.Features.Identity;

public sealed record PreferencesDto(string Theme, bool ReducedMotion, string? DefaultModelId);
public sealed record MeDto(Guid Id, string Account, string DisplayName, PreferencesDto Preferences, string CsrfToken, Guid? ActiveRunId, AiNexus.Features.AccessControl.AccessDto Access);
