using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Assistant;

internal static class AssistantErrors
{
    public static readonly Error QuestionInvalid = Error.Invalid("assistant_question_invalid");

    /// <summary>提問者沒有任何一個帶助理工具的功能：沒有資料可以回答。</summary>
    public static readonly Error Unavailable = Error.Forbidden("assistant_unavailable");
}
