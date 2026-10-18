using AiNexus.Data.App.Library;

namespace AiNexus.Features.Library;

public sealed record PromptTemplateDto(Guid Id, string Title, string Content, DateTimeOffset UpdatedAt);

internal static class PromptTemplateDtoMapping
{
    public static PromptTemplateDto ToDto(this PromptTemplate template) => new(template.Id, template.Title, template.Content, template.UpdatedAt);
}
