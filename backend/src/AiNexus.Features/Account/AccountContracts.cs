using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Account;

public sealed record MeDto(Guid Id, string Account, string DisplayName, PreferencesDto Preferences, string CsrfToken, Guid? ActiveRunId, AccessDto Access);
