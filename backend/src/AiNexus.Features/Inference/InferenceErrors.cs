using AiNexus.Platform.Errors;

namespace AiNexus.Features.Inference;

internal static class InferenceErrors
{
    public const string InputTooLongCode = "input_too_long";
    public static readonly Error RunNotFound = Error.NotFound("run_not_found");
    public static readonly Error InvalidModelPolicy = Error.Invalid("invalid_model_policy");
    public static readonly Error IdempotencyKeyRequired = Error.Invalid("idempotency_key_required");
    public static readonly Error IdempotencyConflict = Error.Conflict("idempotency_conflict");
    public static readonly Error GenerationActive = Error.Conflict("generation_active");
    public static readonly Error QueueFull = new(ErrorKind.RateLimited, "queue_full");
    public static readonly Error KnowledgeSelectionChanged = Error.Conflict("knowledge_selection_changed");
    /// <summary>重新生成對話時自動重複使用原始提問之附件。</summary>
    public static readonly Error RegenerateWithAttachments = Error.Invalid("invalid_request");
    public static readonly Error ModelGroupForbidden = Error.Forbidden("model_group_forbidden");
    public static readonly Error ModelTokenQuota = Error.RateLimited("model_token_quota");
    public static readonly Error TaskInputTooLong = Error.Invalid("task_input_too_long");
    /// <summary>模型正常結束但未產生任何文字內容；此非供應商故障，故重試亦無法解決。</summary>
    public static readonly Error TaskResponseEmpty = Error.Unprocessable("task_response_empty");
    public static readonly Error VisionNotSupported = Error.Invalid("vision_not_supported");
    public static readonly Error ContextBudgetExceeded = Error.Invalid("context_budget_exceeded");
    public static readonly Error ConfigurationChanged = Error.Conflict("evaluation_configuration_changed");
    public static readonly Error ModelSelectionDisabled = Error.Invalid("model_selection_disabled");
    public static readonly Error ProviderUnavailable = Error.Unavailable("provider_unavailable");
    public static readonly Error ModelNotAllowed = Error.Invalid("model_not_allowed");
    public static readonly Error ReasoningNotSupported = Error.Invalid("reasoning_not_supported");
    public static readonly Error AdminResourceNotFound = Error.NotFound("admin_resource_not_found");
}
