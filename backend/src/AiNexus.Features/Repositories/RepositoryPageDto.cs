namespace AiNexus.Features.Repositories;

public sealed record RepositoryPageDto(IReadOnlyList<RepositoryDto> Items, int Page, bool HasMore);
