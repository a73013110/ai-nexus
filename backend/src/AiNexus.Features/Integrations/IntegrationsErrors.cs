using AiNexus.Platform.Errors;

namespace AiNexus.Features.Integrations;

internal static class IntegrationsErrors
{
    public static readonly Error UnknownSource = Error.NotFound("source_unknown");
    public static readonly Error FeatureRevoked = Error.Forbidden("integration_feature_revoked");
    public static readonly Error TransportUnsupported = Error.Unavailable("source_transport_unsupported");
    public static readonly Error NotConfigured = Error.Unavailable("source_not_configured");
    public static readonly Error SourceForbidden = Error.Forbidden("source_forbidden");
    public static readonly Error IdentityMissing = Error.Forbidden("source_identity_missing");
    public static readonly Error SearchInvalid = Error.Invalid("source_search_invalid");
    public static readonly Error RecordIdInvalid = Error.Invalid("source_id_invalid");
    public static readonly Error RecordMissing = Error.NotFound("source_record_missing");
    public static readonly Error ContractInvalid = new(ErrorKind.Upstream, "source_contract_invalid");
    public static readonly Error Changed = Error.Conflict("source_changed");
    public static readonly Error BodyTooLarge = Error.Conflict("source_body_too_large");
    public static readonly Error ArtifactFeatureRequired = Error.Forbidden("artifact_feature_required");
    public static readonly Error ChatFeatureRequired = Error.Forbidden("chat_feature_required");
    public static readonly Error ChatTooLong = Error.Conflict("source_chat_too_long");
}
