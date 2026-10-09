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
    /// <summary>A regeneration reuses the original prompt's attachments.</summary>
    public static readonly Error RegenerateWithAttachments = Error.Invalid("invalid_request");
}
