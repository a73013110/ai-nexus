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
    public static readonly Error FileSizeLimit = Error.TooLarge("file_size_limit");
    public static readonly Error InvalidFileName = Error.Invalid("invalid_file_name");
    public static readonly Error FileSizeMismatch = Error.Invalid("file_size_mismatch");
    public static readonly Error UploadExpired = Error.Conflict("attachment_upload_expired");
    public static readonly Error Quota = Error.TooLarge("attachment_quota");
    public static readonly Error AttachmentLimit = Error.Invalid("attachment_limit");
    public static readonly Error ProcessingRequired = Error.Conflict("document_processing_required");
    public static readonly Error TotalLimit = Error.TooLarge("attachment_total_limit");
    public static readonly Error FileTypeUnsupported = Error.Invalid("file_type_unsupported");
    public static readonly Error InvalidImage = Error.Invalid("invalid_image");
    public static readonly Error PdfPageLimit = Error.Invalid("pdf_page_limit");
    public static readonly Error DocumentHasNoText = Error.Invalid("document_has_no_text");
    public static readonly Error DocumentUnreadable = Error.Invalid("document_unreadable");
    public static readonly Error DocumentTooLarge = Error.Invalid("document_too_large");
    public static readonly Error OfficeArchiveLimit = Error.Invalid("office_archive_limit");
    public static readonly Error OfficeMacrosUnsupported = Error.Invalid("office_macros_unsupported");
    public static readonly Error PresentationSlideLimit = Error.Invalid("presentation_slide_limit");
    public static readonly Error SpreadsheetStringLimit = Error.Invalid("spreadsheet_string_limit");
    public static readonly Error SpreadsheetSheetLimit = Error.Invalid("spreadsheet_sheet_limit");
    public static readonly Error SpreadsheetCellLimit = Error.Invalid("spreadsheet_cell_limit");
    public static readonly Error ContentInvalid = Error.Unavailable("attachment_content_invalid");
    public static readonly Error ContentMissing = Error.Unavailable("attachment_content_missing");
}
