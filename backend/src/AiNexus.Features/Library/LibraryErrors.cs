using AiNexus.Platform.Errors;

namespace AiNexus.Features.Library;

internal static class LibraryErrors
{
    public static readonly Error NotFound = Error.NotFound("template_not_found");
    public static readonly Error LimitReached = Error.Invalid("template_limit");
}
