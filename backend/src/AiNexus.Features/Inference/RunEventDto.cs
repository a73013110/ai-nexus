namespace AiNexus.Features.Inference;

public sealed record RunEventDto(int Version, long Sequence, Guid RunId, string Type, string Status, string? Delta, string? ErrorCode, string? IssueCode = null);
