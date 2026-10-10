using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality;

internal static class QualityErrors
{
    public static readonly Error ItemMissing = Error.NotFound("quality_item_missing");
    public static readonly Error AnswerPending = Error.Conflict("feedback_answer_pending");
    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error SetConflict = Error.Conflict("evaluation_set_conflict");
    public static readonly Error SetLimit = Error.Conflict("evaluation_set_limit");
    public static readonly Error EvaluationActive = Error.Conflict("evaluation_active");
    public static readonly Error RunLimit = Error.Conflict("evaluation_run_limit");
    public static readonly Error RetrievalMissing = Error.NotFound("retrieval_evaluation_missing");
    public static readonly Error CorpusInvalid = Error.Invalid("retrieval_corpus_invalid");
    public static readonly Error PagesInvalid = Error.Invalid("retrieval_pages_invalid");
    public static readonly Error RetrievalActive = Error.Conflict("retrieval_evaluation_active");
    public static readonly Error RetrievalLimit = Error.Conflict("retrieval_evaluation_limit");
    public static readonly Error RetrievalChanged = Error.Conflict("retrieval_evaluation_changed");
    public static readonly Error AccessRevoked = Error.Forbidden("evaluation_access_revoked");
    public static readonly Error RunMissing = Error.NotFound("evaluation_run_missing");
}
