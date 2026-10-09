namespace AiNexus.Features.Repositories;

public sealed record RepositoryFileDto(string Repository, string Commit, string Path, string Text, string Url);
