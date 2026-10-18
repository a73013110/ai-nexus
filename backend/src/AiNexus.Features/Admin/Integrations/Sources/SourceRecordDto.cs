namespace AiNexus.Features.Admin.Integrations.Sources;

public sealed record SourceRecordDto(string Id, string Kind, string Title, string Status, string Revision, DateTimeOffset ModifiedAt);
