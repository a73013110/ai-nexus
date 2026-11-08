using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Scheduling;

public enum TaskOutcomeKind
{
    Succeeded,

    /// <summary>執行成功但沒事可做；紀錄保存期較短，管理頁可隱藏。</summary>
    Idle,

    Failed,
}

/// <summary>一輪的結果。<see cref="Summary"/> 是給管理員看的一句話（例如「清除 120 筆」），不放個人資料。</summary>
public sealed record TaskOutcome(TaskOutcomeKind Kind, string? Summary, Error? Error)
{
    public static TaskOutcome Done(string? summary = null) => new(TaskOutcomeKind.Succeeded, summary, null);

    public static TaskOutcome Idle(string? summary = null) => new(TaskOutcomeKind.Idle, summary, null);

    public static TaskOutcome Failed(Error error, string? summary = null) => new(TaskOutcomeKind.Failed, summary, error);

    /// <summary>依處理筆數決定：0 筆是 idle，其餘以 <paramref name="summary"/> 描述。</summary>
    public static TaskOutcome Count(int processed, Func<int, string> summary) => processed == 0 ? Idle() : Done(summary(processed));

    public static implicit operator TaskOutcome(Error error) => Failed(error);
}
