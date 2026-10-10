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
    public static readonly Error InvalidUser = Error.Invalid("invalid_user");
    public static readonly Error InvalidLoginMethods = Error.Invalid("invalid_login_methods");
    public static readonly Error InvalidPassword = Error.Invalid("invalid_password");
    public static readonly Error LocalPasswordRequired = Error.Invalid("local_password_required");
    public static readonly Error InvalidRoles = Error.Invalid("invalid_roles");
    public static readonly Error UserNotFound = Error.NotFound("user_not_found");
    public static readonly Error AccountExists = Error.Conflict("account_exists");
    public static readonly Error AdBindingImmutable = Error.Conflict("ad_binding_immutable");
}
