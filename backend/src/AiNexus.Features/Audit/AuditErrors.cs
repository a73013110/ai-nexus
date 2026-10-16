using AiNexus.Platform.Errors;

namespace AiNexus.Features.Audit;

internal static class AuditErrors
{
    public static readonly Error InvalidFilter = Error.Invalid("invalid_audit_filter");
    public static readonly Error ExportLimit = Error.Invalid("audit_export_limit");
}
