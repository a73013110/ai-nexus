using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Audit;

internal static class AuditErrors
{
    public static readonly Error InvalidFilter = Error.Invalid("invalid_audit_filter");
    public static readonly Error ExportLimit = Error.Invalid("audit_export_limit");
    public static readonly Error FeatureNotFound = Error.NotFound("audit_feature_not_found");
}
