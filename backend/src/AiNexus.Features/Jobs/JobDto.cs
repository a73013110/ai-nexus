
namespace AiNexus.Features.Jobs;

public sealed record JobDto(Guid Id, string Kind, Guid SubjectId, string Label, string Status, string Stage, int Attempt, int CompletedUnits, int? TotalUnits, bool CancelRequested, string? ErrorCode, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? IssueCode = null);
