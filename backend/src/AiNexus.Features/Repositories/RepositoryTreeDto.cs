namespace AiNexus.Features.Repositories;

public sealed record RepositoryTreeDto(string Repository, string Commit, string Path, IReadOnlyList<RepositoryEntryDto> Entries);
