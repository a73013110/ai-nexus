namespace AiNexus.Features.Admin.Integrations.Handoff;

public sealed record SourceImportRequest(string RecordId, string ExpectedRevision);
