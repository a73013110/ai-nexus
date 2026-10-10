using AiNexus.Platform.Errors;

namespace AiNexus.Features.Collaboration;

/// <summary>Public because every module that shares resources answers with these codes.</summary>
public static class CollaborationErrors
{
    public static readonly Error ResourceNotFound = Error.NotFound("resource_not_found");
    public static readonly Error ResourceReadOnly = Error.Forbidden("resource_read_only");
    public static readonly Error OwnerIsImplicit = Error.Invalid("owner_is_implicit");
    public static readonly Error UnknownMember = Error.Invalid("unknown_resource_member");
    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error TasksActive = Error.Conflict("resource_tasks_active");
}
