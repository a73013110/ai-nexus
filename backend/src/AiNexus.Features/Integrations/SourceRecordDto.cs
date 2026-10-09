namespace AiNexus.Features.Integrations;

public sealed record SourceRecordDto(string Id, string Kind, string Title, string Status, string Revision, DateTimeOffset ModifiedAt);
