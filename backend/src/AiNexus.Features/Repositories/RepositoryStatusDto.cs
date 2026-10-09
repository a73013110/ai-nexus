namespace AiNexus.Features.Repositories;

public sealed record RepositoryStatusDto(bool Available, bool Connected, string BaseUrl, string? Login, string Notice);
