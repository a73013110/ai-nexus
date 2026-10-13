namespace AiNexus.Features.Repositories.Browsing;

public sealed record RepositoryDto(string FullName, string Description, bool Private, bool Archived, bool Empty, string DefaultBranch, string Language,
    int Stars, int Forks, int OpenIssues, int OpenPulls, DateTimeOffset? UpdatedAt, string Url, bool Pinned);
