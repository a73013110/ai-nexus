using AiNexus.Platform.Errors;

namespace AiNexus.Features.Attachments;

internal static class AttachmentsErrors
{
    public const string FileNameInvalidCode = "file_name_invalid";

    public static readonly Error NotFound = Error.NotFound("attachment_not_found");
    public static readonly Error InUse = Error.Conflict("attachment_in_use");
    public static readonly Error MultipartRequired = Error.Invalid("multipart_required");
    public static readonly Error FileRequired = Error.Invalid("file_required");
    public static readonly Error LibraryFileNotFound = Error.NotFound("file_not_found");
    public static readonly Error ExtensionChanged = Error.Invalid("file_extension_changed");
    public static readonly Error FileNameChanged = Error.Conflict("file_name_changed");
    public static readonly Error FilterInvalid = Error.Invalid("file_filter_invalid");
}
