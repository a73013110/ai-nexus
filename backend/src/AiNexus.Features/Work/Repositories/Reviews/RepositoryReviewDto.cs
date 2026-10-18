using AiNexus.Features.Work.Jobs;

namespace AiNexus.Features.Work.Repositories.Reviews;

public sealed record RepositoryReviewDto(Guid Id, string Repository, string Commit, string? BaseCommit, int? PullRequest, string Purpose, string ModelId, string ModelDisplayName,
    string Note, DateTimeOffset CreatedAt, JobDto Job, string? CommentUrl);
public sealed record RepositoryReviewSectionDto(int Ordinal, string Label, string Diff, bool Binary, string? Output, bool Truncated, long? InputTokens, long? OutputTokens, long? ElapsedMs);
public sealed record RepositoryReviewReportDto(string Output, bool Truncated, long? InputTokens, long? OutputTokens, long ElapsedMs);
public sealed record RepositoryReviewDetailDto(RepositoryReviewDto Review, IReadOnlyList<RepositoryReviewSectionDto> Sections, RepositoryReviewReportDto? Report);
