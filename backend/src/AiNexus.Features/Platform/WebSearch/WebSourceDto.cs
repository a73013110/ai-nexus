namespace AiNexus.Features.Platform.WebSearch;

public sealed record WebSourceDto(int Number, string Title, string Url, string Excerpt, DateTimeOffset RetrievedAt);
