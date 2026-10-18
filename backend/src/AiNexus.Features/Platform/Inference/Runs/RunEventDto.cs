namespace AiNexus.Features.Platform.Inference.Runs;

/// <summary>
/// 生成即時串流事件（SSE）資料傳輸物件。
/// </summary>
public sealed record RunEventDto(
    int Version,
    long Sequence,
    Guid RunId,
    string Type,
    string Status,
    string? Delta,
    string? ErrorCode,
    string? IssueCode = null);
