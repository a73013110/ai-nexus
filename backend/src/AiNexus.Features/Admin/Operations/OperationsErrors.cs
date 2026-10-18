using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Operations;

internal static class OperationsErrors
{
    public static readonly Error AdminResourceNotFound = Error.NotFound("admin_resource_not_found");
    public static readonly Error InvalidSearch = Error.Invalid("invalid_search");
}
