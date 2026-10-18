namespace AiNexus.Features.Work.Jobs;

/// <summary>背景工作對外的檢視資料；ErrorMessage 只含固定的安全提示與查證代碼。</summary>
public sealed record JobDto(
    Guid Id,
    string Kind,
    Guid SubjectId,
    string Label,
    string Status,
    string Stage,
    int Attempt,
    int CompletedUnits,
    int? TotalUnits,
    bool CancelRequested,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? IssueCode = null);
