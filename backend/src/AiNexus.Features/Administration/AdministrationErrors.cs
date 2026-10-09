using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Administration;

internal static class AdministrationErrors
{
    public static readonly Error AdminRequired = Error.Forbidden("admin_required");
    public static readonly Error Lockout = Error.Conflict("admin_lockout");
    public static readonly Error NotFound = Error.NotFound("admin_resource_not_found");
    public static readonly Error InvalidSearch = Error.Invalid("invalid_search");
    public static readonly Error InvalidStorageLimit = Error.Invalid("invalid_storage_limit");
    public static readonly Error InvalidAccessId = Error.Invalid("invalid_access_id");
    public static readonly Error InvalidAccessIds = Error.Invalid("invalid_access_ids");
    public static readonly Error InvalidAccessName = Error.Invalid("invalid_access_name");
    public static readonly Error UnknownAccessId = Error.Invalid("unknown_access_id");
    public static readonly Error InvalidOrder = Error.Invalid("invalid_order");

    /// <summary>For <see cref="AdministrativeAudit.MutateAsync"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}
