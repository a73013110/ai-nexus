namespace AiNexus.Features.Admin.Integrations.Sources;

public sealed record SourceDto(string Id, string Name, string Description, string Status, string Notice, bool CanQuery, IReadOnlyList<string> Kinds);
