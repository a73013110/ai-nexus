namespace AiNexus.Features.Platform.WebSearches;

public sealed record WebSourceDto(int Number, string Title, string Url, string Excerpt, DateTimeOffset RetrievedAt);
