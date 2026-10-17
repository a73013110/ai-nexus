using AiNexus.Platform.Errors;

namespace AiNexus.Features.Collaboration;

/// <summary>刻意公開：所有分享資源的模組都要回傳這些代碼，前端才能一致處理。</summary>
public static class CollaborationErrors
{
    public static readonly Error ResourceNotFound = Error.NotFound("resource_not_found");
    public static readonly Error ResourceReadOnly = Error.Forbidden("resource_read_only");
    public static readonly Error OwnerIsImplicit = Error.Invalid("owner_is_implicit");
    public static readonly Error UnknownMember = Error.Invalid("unknown_resource_member");
    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error TasksActive = Error.Conflict("resource_tasks_active");
}
