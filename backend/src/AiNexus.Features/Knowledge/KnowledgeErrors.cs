using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge;

internal static class KnowledgeErrors
{
    public static readonly Error DocumentNotFound = Error.NotFound("document_not_found");
    public static readonly Error DescriptionTooLong = Error.Invalid("description_too_long");
    public static readonly Error CollectionLimit = Error.Conflict("collection_limit");
    public static readonly Error DocumentLimit = Error.Conflict("document_limit");
    public static readonly Error DocumentNotIndexed = Error.Conflict("document_not_indexed");
    public static readonly Error JobActive = Error.Conflict("job_active");
    public static readonly Error NotEditableText = Error.Conflict("document_not_editable_text");
    public static readonly Error TextTitleInvalid = Error.Invalid("text_title_invalid");
    public static readonly Error TextVersionChanged = Error.Conflict("text_version_changed");
    public static readonly Error DocumentProcessing = Error.Conflict("document_processing");
    public static readonly Error GenerationActive = Error.Conflict("generation_active");

    /// <summary>For <see cref="DocumentService"/>, whose callers in other modules and job handlers can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}
