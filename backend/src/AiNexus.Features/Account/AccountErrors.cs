using AiNexus.Platform.Errors;

namespace AiNexus.Features.Account;

internal static class AccountErrors
{
    public static readonly Error InvalidTheme = Error.Invalid("invalid_theme");
    public static readonly Error InvalidModel = Error.Invalid("invalid_model");
    public static readonly Error ModelNotAllowed = Error.Invalid("model_not_allowed");
}
