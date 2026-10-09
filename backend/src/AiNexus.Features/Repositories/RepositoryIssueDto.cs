namespace AiNexus.Features.Repositories;

public sealed record RepositoryIssueDto(int Number, string Title, string Body, string State, string Url);
