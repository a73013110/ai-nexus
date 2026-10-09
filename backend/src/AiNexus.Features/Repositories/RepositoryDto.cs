namespace AiNexus.Features.Repositories;

public sealed record RepositoryDto(string FullName, string Description, bool Private, string DefaultBranch, string Url);
