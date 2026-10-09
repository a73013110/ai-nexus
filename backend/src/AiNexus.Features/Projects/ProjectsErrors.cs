using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects;

internal static class ProjectsErrors
{
    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error Limit = Error.Conflict("project_limit");
    public static readonly Error Conflict = Error.Conflict("project_conflict");
    public static readonly Error Archived = Error.Conflict("project_archived");
    public static readonly Error FileLimit = Error.Conflict("project_file_limit");
    public static readonly Error TemplateMissing = Error.NotFound("template_missing");
    public static readonly Error TemplateLimit = Error.Conflict("template_limit");
    public static readonly Error ChatAccessRequired = Error.Forbidden("chat_access_required");
    public static readonly Error AccessRequired = Error.Forbidden("project_access_required");
    public static readonly Error ConversationMissing = Error.NotFound("conversation_missing");
    public static readonly Error GenerationActive = Error.Conflict("generation_active");

    /// <summary>For <see cref="ProjectService"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}
