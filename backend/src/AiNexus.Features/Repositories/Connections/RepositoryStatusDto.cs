namespace AiNexus.Features.Repositories.Connections;

public sealed record RepositoryStatusDto(bool Available, bool Connected, string BaseUrl, string? Login, DateTimeOffset? ConnectedAt);
