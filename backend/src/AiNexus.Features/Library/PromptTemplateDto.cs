namespace AiNexus.Features.Library;

public sealed record PromptTemplateDto(Guid Id, string Title, string Content, DateTimeOffset UpdatedAt);
