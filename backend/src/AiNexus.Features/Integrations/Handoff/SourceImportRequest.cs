namespace AiNexus.Features.Integrations.Handoff;

public sealed record SourceImportRequest(string RecordId, string ExpectedRevision);
