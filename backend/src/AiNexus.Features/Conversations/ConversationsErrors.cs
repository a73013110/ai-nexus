using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Conversations;

internal static class ConversationsErrors
{
    public static readonly Error NotFound = Error.NotFound("conversation_not_found");
    public static readonly Error InvalidQuery = Error.Invalid("invalid_query");
    public static readonly Error InvalidTitle = Error.Invalid(InvalidTitleCode);
    public static readonly Error InvalidLabels = Error.Invalid("invalid_labels");
    public static readonly Error InvalidBackup = Error.Invalid(InvalidBackupCode);
    public static readonly Error GenerationActive = Error.Conflict("generation_active");
    public static readonly Error MessageNotFound = Error.NotFound("message_not_found");

    public const string InvalidTitleCode = "invalid_title";
    public const string InstructionTooLongCode = "instruction_too_long";
    public const string InvalidBackupCode = "invalid_backup";

    /// <summary>For <see cref="ConversationService"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}
