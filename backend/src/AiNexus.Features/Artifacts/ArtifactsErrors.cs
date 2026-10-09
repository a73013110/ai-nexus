using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

internal static class ArtifactsErrors
{
    public const string ContentInvalidCode = "artifact_content_invalid";
    public const string TransformInputInvalidCode = "transform_input_invalid";

    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error ContentInvalid = Error.Invalid(ContentInvalidCode);
    public static readonly Error VersionMissing = Error.NotFound("artifact_version_missing");
    public static readonly Error MessageNotFound = Error.NotFound("message_not_found");
    public static readonly Error LimitReached = Error.Conflict("artifact_limit");
    public static readonly Error ProjectAccessRequired = Error.Forbidden("project_access_required");
    public static readonly Error VersionLimit = Error.Conflict("artifact_version_limit");
    public static readonly Error VersionConflict = Error.Conflict("artifact_version_conflict");
    public static readonly Error ExportFormatInvalid = Error.Invalid("export_format_invalid");
    public static readonly Error TransformActionInvalid = Error.Invalid("transform_action_invalid");
}
