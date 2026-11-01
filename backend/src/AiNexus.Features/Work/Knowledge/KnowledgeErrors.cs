using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge;

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
    public static readonly Error TextContentInvalid = Error.Invalid("text_content_invalid");
    public static readonly Error QueryInvalid = Error.Invalid("knowledge_query_invalid");
    public static readonly Error RetrievalModeInvalid = Error.Invalid("retrieval_mode_invalid");
    public static readonly Error ProfileChanged = Error.Conflict("embedding_profile_changed");
    public static readonly Error PromptRequired = Error.Invalid("prompt_required");
    public static readonly Error InvalidHistory = Error.Conflict("invalid_history");
    public static readonly Error SelectionLimit = Error.Invalid("knowledge_collection_limit");
    public static readonly Error SourceChanged = Error.Conflict("knowledge_source_changed");
    public static readonly Error ProfileNotFound = Error.NotFound("profile_not_found");
    public static readonly Error ProfileRetired = Error.Conflict("profile_retired");
    public static readonly Error ProfileNotBuilding = Error.Conflict("profile_not_building");
    public static readonly Error ProfileIncomplete = Error.Conflict("profile_incomplete");
    public static readonly Error ProfileNotRetired = Error.Conflict("profile_not_retired");
    public static readonly Error DocumentPagesMissing = Error.Conflict("document_pages_missing");
    public static readonly Error PdfPageLimit = Error.Invalid("pdf_page_limit");
    public static readonly Error DocumentTooLarge = Error.Invalid("document_too_large");
    public static readonly Error DocumentHasNoText = Error.Unprocessable("document_has_no_text");
    public static readonly Error OcrPageLayoutUnsupported = Error.Unprocessable("ocr_page_layout_unsupported");
    public static readonly Error OcrImageUnsupported = Error.Unprocessable("ocr_image_unsupported");
    public static readonly Error OcrOutputTruncated = Error.Unprocessable("ocr_output_truncated");
    public static readonly Error IndexSourceChanged = Error.Conflict("index_source_changed");
    public static readonly Error TableRowTooLong = Error.Unprocessable("table_row_too_long");
    public static readonly Error EmbeddingTimeout = Error.Timeout("embedding_timeout");
    public static readonly Error EmbeddingInvalid = Error.Upstream("embedding_invalid");
    public static readonly Error EmbeddingsDisabled = Error.Conflict("embeddings_disabled");
    public static readonly Error RetrievalProviderRateLimited = Error.RateLimited("retrieval_provider_unavailable");
    public static readonly Error RetrievalProviderUnavailable = Error.Unavailable("retrieval_provider_unavailable");
    public static readonly Error EmbeddingDailyQuota = Error.RateLimited("embedding_daily_quota");
    public static readonly Error RewriteModelUnavailable = Error.Unavailable("rewrite_model_unavailable");
    public static readonly Error RewriteInvalid = Error.Upstream("rewrite_invalid");
    public static readonly Error RerankInvalid = Error.Upstream("rerank_invalid");
    public static readonly Error EmbeddingProbeInvalid = Error.Upstream("embedding_probe_invalid");
    public static readonly Error RerankProbeInvalid = Error.Upstream("rerank_probe_invalid");
    public static readonly Error RerankDisabled = Error.Unavailable("rerank_disabled");
    public static readonly Error RerankUnavailable = Error.Unavailable("rerank_unavailable");
    public static readonly Error FullTextUnavailable = Error.Unavailable("fulltext_unavailable");
}
