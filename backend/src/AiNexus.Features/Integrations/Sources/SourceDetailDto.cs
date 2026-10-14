namespace AiNexus.Features.Integrations.Sources;

public sealed record SourceHistoryDto(DateTimeOffset At, string Kind, string Actor, string Description, string Revision);

public sealed record SourceDetailDto(string SourceId, SourceRecordDto Record, string Body, IReadOnlyList<SourceHistoryDto> History, bool HistoryLimited);
