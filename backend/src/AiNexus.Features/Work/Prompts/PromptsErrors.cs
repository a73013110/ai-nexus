using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Prompts;

internal static class PromptsErrors
{
    public static readonly Error NotFound = Error.NotFound("template_not_found");
    public static readonly Error LimitReached = Error.Invalid("template_limit");
}
