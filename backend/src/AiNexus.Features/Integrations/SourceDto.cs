namespace AiNexus.Features.Integrations;

public sealed record SourceDto(string Id, string Name, string Description, string Status, string Notice, bool CanQuery, IReadOnlyList<string> Kinds);
