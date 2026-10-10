using AiNexus.Platform.Errors;

namespace AiNexus.Features.Diagnostics;

internal static class DiagnosticsErrors
{
    public static readonly Error FilterInvalid = Error.Invalid("log_filter_invalid");
    public static readonly Error TextRange = Error.Invalid("log_text_range");
    public static readonly Error CursorInvalid = Error.Invalid("log_cursor_invalid");
    public static readonly Error NotFound = Error.NotFound("log_not_found");
    public static readonly Error ExportCursor = Error.Invalid("log_export_cursor");
    public static readonly Error ExportLimit = Error.Invalid("log_export_limit");
}
