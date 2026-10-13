namespace AiNexus.Features.Repositories.Browsing;

public sealed record RepositoryFileDto(string Commit, string Path, string Text, long Size, string Url);
